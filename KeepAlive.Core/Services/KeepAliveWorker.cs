using System.Threading.Channels;
using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

/// <summary>
/// 設定間隔でキープアライブ通信を繰り返す。
/// 開始直後に 1 回実行し、以降は「前回完了から Interval 後」に実行する。
/// 設定変更・スリープ復帰・<see cref="TriggerNow"/> では待機を打ち切って即時実行する。
/// 動作中は通信量を計測し、自動停止日時か通信量の上限に達したら停止する。
/// </summary>
public sealed class KeepAliveWorker : IKeepAliveWorker, IDisposable
{
    private readonly IKeepAliveStrategyResolver _resolver;
    private readonly ISettingsService _settings;
    private readonly IDataUsageTracker _dataUsage;
    private readonly ILogService _log;
    private readonly IPowerEventSource _power;
    private readonly ISleepInhibitor _sleepInhibitor;
    private readonly TimeProvider _timeProvider;
    private readonly KeepAliveOptions _options;

    private readonly object _gate = new();

    /// <summary>スリープの抑止の切り替えを 1 つずつ行うためのロック (<see cref="_gate"/> の中では取らない)。</summary>
    private readonly object _sleepGate = new();
    private bool _sleepInhibited;
    private readonly Channel<bool> _wake =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private CancellationTokenSource? _runCts;
    private Task? _loopTask;
    private KeepAliveStatistics _statistics = KeepAliveStatistics.Empty;
    private DateTimeOffset? _nextRunAt;
    private AppSettings _lastSettings;

    /// <summary>待機の打ち切りが通信の要求か (1)、自動停止の条件の再計算のみか (0)。</summary>
    private int _runRequested;

    /// <summary>
    /// 毎週の自動停止の次回を、この日時より後から探す。開始時と、毎週の自動停止の設定を変えたときに更新する
    /// (動作中に時刻を変えたとき、変える前に過ぎた時刻ですぐ止まらないようにするため)。
    /// </summary>
    private DateTimeOffset _weeklyStopBase;

    /// <summary>
    /// <see cref="_weeklyStopBase"/> を決めたときの毎週の自動停止の設定。
    /// ループが新しい設定を読んだ時点で違っていれば、その時点を基準にし直す
    /// (設定の変更通知より先にループが新しい設定を読んでも、古い基準で判定しないようにするため)。
    /// </summary>
    private (bool Enabled, TimeOnly Time, WeeklyDays Days) _weeklyStopKey;

    /// <summary>自動停止の日時とその理由。</summary>
    private readonly record struct ScheduledStop(DateTimeOffset At, AutoStopReason Reason);

    public KeepAliveWorker(
        IKeepAliveStrategyResolver resolver,
        ISettingsService settings,
        IDataUsageTracker dataUsage,
        ILogService log,
        IPowerEventSource power,
        ISleepInhibitor sleepInhibitor,
        TimeProvider timeProvider,
        KeepAliveOptions options)
    {
        _resolver = resolver;
        _settings = settings;
        _dataUsage = dataUsage;
        _log = log;
        _power = power;
        _sleepInhibitor = sleepInhibitor;
        _timeProvider = timeProvider;
        _options = options;
        _lastSettings = settings.Current;

        _settings.SettingsChanged += OnSettingsChanged;
        _power.Suspending += OnSuspending;
        _power.Resumed += OnResumed;
    }

    public bool IsRunning
    {
        get { lock (_gate) return _loopTask is not null; }
    }

    public KeepAliveStatistics Statistics
    {
        get { lock (_gate) return _statistics; }
    }

    public DateTimeOffset? NextRunAt
    {
        get { lock (_gate) return _nextRunAt; }
    }

    public event EventHandler? StateChanged;

    public event EventHandler<KeepAliveResult>? ResultReceived;

    public event EventHandler<AutoStopReason>? AutoStopped;

    public void Start()
    {
        var s = _settings.Current;
        // 上限に達したまま開始しても、開始直後に止まるだけになるため開始しない
        if (!IsRunning && IsDataLimitReached(s))
        {
            _log.Warning(
                $"通信量の累計 ({DataUsage.Format(_dataUsage.Current.TotalBytes)}) が上限 ({s.DataLimitMegabytes} MB) に達しているため、開始できません。" +
                "モニター画面で通信量をリセットするか、設定画面で上限を変更してください。");
            return;
        }

        var startedAt = _timeProvider.GetUtcNow();
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_loopTask is not null)
                return;

            while (_wake.Reader.TryRead(out _)) { }
            _runRequested = 0;
            _weeklyStopBase = startedAt;
            _weeklyStopKey = WeeklyStopKey(s);
            var runCts = _runCts = new CancellationTokenSource();
            var token = runCts.Token;
            // 「開始」ログが初回結果より先に出るよう、ループ本体は開始通知の後に走らせる
            _loopTask = Task.Run(async () =>
            {
                await startSignal.Task.ConfigureAwait(false);
                try
                {
                    if (await RunAsync(startedAt, token).ConfigureAwait(false) is { } reason)
                        _ = AutoStopAsync(runCts, reason);
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    // ループが黙って止まり「動作中」の表示だけが残るのを防ぐため、停止状態に戻す
                    _log.Error($"予期しないエラーのためキープアライブを停止しました: {ex.GetType().Name}: {ex.Message}");
                    _ = StopCoreAsync(runCts);
                }
            }, CancellationToken.None);
        }

        try
        {
            _log.Info($"キープアライブを開始しました ({_resolver.Resolve(s.Method).Name} / {s.IntervalSeconds} 秒間隔)。");
            if (s.AutoStopAt is { } stopAt)
            {
                if (stopAt > startedAt)
                    _log.Info($"{stopAt.ToLocalTime():yyyy/MM/dd HH:mm} に自動停止します。");
                else
                    _log.Warning($"自動停止日時 ({stopAt.ToLocalTime():yyyy/MM/dd HH:mm}) を過ぎているため、自動停止は行いません。");
            }

            LogWeeklyStop(s, startedAt);

            if (s.DataLimitBytes is not null)
                _log.Info($"通信量の累計が {s.DataLimitMegabytes} MB に達したら自動停止します (現在 {DataUsage.Format(_dataUsage.Current.TotalBytes)})。");

            UpdateSleepInhibition();
            RaiseStateChanged();
        }
        finally
        {
            startSignal.SetResult();
        }
    }

    public Task StopAsync() => StopCoreAsync(expected: null);

    /// <param name="expected">
    /// 指定した場合、その実行が現在も続いているときだけ停止する (自動停止と手動の停止・再開との競合対策)。
    /// </param>
    private async Task StopCoreAsync(CancellationTokenSource? expected)
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            if (expected is not null && !ReferenceEquals(_runCts, expected))
                return;

            cts = _runCts;
            loop = _loopTask;
            _runCts = null;
            _loopTask = null;
            _nextRunAt = null;
        }

        if (cts is null || loop is null)
            return;

        cts.Cancel();
        try
        {
            await loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            // 停止 (アプリ終了を含む) が失敗しないよう、ここでは例外を外に出さない
            _log.Error($"停止処理中にエラーが発生しました: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            cts.Dispose();
        }

        _log.Info("キープアライブを停止しました。");
        UpdateSleepInhibition();
        RaiseStateChanged();
    }

    /// <summary>
    /// 動作中で、設定でスリープの抑止がオンなら抑止し、それ以外なら解除する。
    /// 開始・停止・設定の変更のたびに呼ぶ。失敗してもキープアライブは止めず、ログに残すだけにする。
    /// </summary>
    private void UpdateSleepInhibition()
    {
        if (!_sleepInhibitor.IsSupported)
            return;

        lock (_sleepGate)
        {
            var inhibit = IsRunning && _settings.Current.PreventSleepWhileRunning;
            if (inhibit == _sleepInhibited)
                return;

            try
            {
                _sleepInhibitor.SetEnabled(inhibit);
                _sleepInhibited = inhibit;
                _log.Info(inhibit
                    ? "動作中は、操作しないときの自動スリープをしないようにしました。"
                    : "自動スリープの抑止を解除しました。");
            }
            catch (Exception ex)
            {
                _log.Warning(inhibit
                    ? $"自動スリープを抑止できませんでした: {ex.Message}"
                    : $"自動スリープの抑止を解除できませんでした: {ex.Message}");
            }
        }
    }

    public void TriggerNow()
    {
        Volatile.Write(ref _runRequested, 1);
        _wake.Writer.TryWrite(true);
    }

    public void ResetStatistics()
    {
        lock (_gate)
        {
            _statistics = KeepAliveStatistics.Empty;
        }
    }

    public void Dispose()
    {
        _settings.SettingsChanged -= OnSettingsChanged;
        _power.Suspending -= OnSuspending;
        _power.Resumed -= OnResumed;

        lock (_gate)
        {
            _runCts?.Cancel();
        }

        // 停止を待たずに終わる場合も、スリープの抑止は残さない
        lock (_sleepGate)
        {
            if (!_sleepInhibited)
                return;

            try
            {
                _sleepInhibitor.SetEnabled(false);
                _sleepInhibited = false;
            }
            catch (Exception ex)
            {
                _log.Warning($"自動スリープの抑止を解除できませんでした: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 停止されるまで通信を繰り返す。自動停止の条件を満たした場合は、その理由を返して終了する。
    /// </summary>
    private async Task<AutoStopReason?> RunAsync(DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var dataUsage = _dataUsage.StartMeasurement();
        try
        {
            DateTimeOffset? lastRunAt = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                var settings = _settings.Current;
                var now = _timeProvider.GetUtcNow();

                // スリープ中に日時を過ぎた場合も、復帰後のこの判定で通信せずに停止する
                if (GetNextAutoStop(settings, startedAt) is { } stop && now >= stop.At)
                    return stop.Reason;
                if (IsDataLimitReached(settings))
                    return AutoStopReason.DataLimit;

                // 電源イベントを取得できない OS 向けの保険: 想定より大きく時間が飛んでいればスリープ等とみなす
                if (lastRunAt is { } last && now - last > settings.Interval * 2 + settings.Timeout)
                    _log.Warning($"前回実行から {(now - last).TotalMinutes:F1} 分経過しています (スリープ／休止からの復帰の可能性)。");
                lastRunAt = now;

                await ExecuteOnceAsync(settings, cancellationToken).ConfigureAwait(false);

                var interval = _settings.Current.Interval;
                var nextRunAt = _timeProvider.GetUtcNow() + interval;
                lock (_gate)
                {
                    if (!cancellationToken.IsCancellationRequested)
                        _nextRunAt = _timeProvider.GetLocalNow() + interval;
                }

                if (await WaitForNextRunAsync(nextRunAt, startedAt, dataUsage, cancellationToken).ConfigureAwait(false) is { } reason)
                    return reason;
            }

            return null;
        }
        finally
        {
            // 停止までの通信量を数えて保存する
            await dataUsage.DisposeAsync().ConfigureAwait(false);
        }
    }

    private bool IsDataLimitReached(AppSettings settings) =>
        settings.DataLimitBytes is { } limit && _dataUsage.Current.TotalBytes >= limit;

    /// <summary>
    /// この実行で有効な自動停止日時。開始時点で既に過ぎていた日時は対象外とする
    /// (前回終了後に過ぎた日時で、次回起動時に即停止してしまうのを防ぐ)。
    /// </summary>
    private static DateTimeOffset? GetActiveAutoStopAt(AppSettings settings, DateTimeOffset startedAt) =>
        settings.AutoStopAt is { } stopAt && stopAt > startedAt ? stopAt : null;

    /// <summary>この実行で次に来る、毎週の自動停止の日時。無効なら null。</summary>
    private DateTimeOffset? GetNextWeeklyStop(AppSettings settings)
    {
        if (!settings.WeeklyStopEnabled)
            return null;

        DateTimeOffset after;
        lock (_gate)
        {
            // 動作中に毎週の設定が変わったら、変わったのを見た時点から数え直す
            // (変える前に過ぎた時刻で、すぐに止まらないようにする)
            var key = WeeklyStopKey(settings);
            if (key != _weeklyStopKey)
            {
                _weeklyStopKey = key;
                _weeklyStopBase = _timeProvider.GetUtcNow();
            }

            after = _weeklyStopBase;
        }

        return WeeklySchedule.GetNextOccurrence(
            settings.WeeklyStopDays, settings.WeeklyStopTime, after, _timeProvider.LocalTimeZone);
    }

    private static (bool Enabled, TimeOnly Time, WeeklyDays Days) WeeklyStopKey(AppSettings settings) =>
        (settings.WeeklyStopEnabled, settings.WeeklyStopTime, settings.WeeklyStopDays);

    /// <summary>
    /// この実行で次に来る自動停止 (日時の予約と毎週の自動停止のうち早い方)。
    /// 同じ日時のときは、予約を解除できるよう日時の予約として扱う。
    /// </summary>
    private ScheduledStop? GetNextAutoStop(AppSettings settings, DateTimeOffset startedAt)
    {
        var scheduled = GetActiveAutoStopAt(settings, startedAt);
        var weekly = GetNextWeeklyStop(settings);
        if (scheduled is { } s && (weekly is null || s <= weekly))
            return new ScheduledStop(s, AutoStopReason.ScheduledTime);
        if (weekly is { } w)
            return new ScheduledStop(w, AutoStopReason.WeeklySchedule);
        return null;
    }

    private string ToLocalText(DateTimeOffset at) =>
        $"{TimeZoneInfo.ConvertTime(at, _timeProvider.LocalTimeZone):yyyy/MM/dd HH:mm}";

    private void LogWeeklyStop(AppSettings settings, DateTimeOffset startedAt)
    {
        if (!settings.WeeklyStopEnabled)
            return;

        var next = WeeklySchedule.GetNextOccurrence(
            settings.WeeklyStopDays, settings.WeeklyStopTime, startedAt, _timeProvider.LocalTimeZone);
        if (next is { } at)
            _log.Info($"毎週の自動停止 ({WeeklySchedule.Format(settings.WeeklyStopDays)} {settings.WeeklyStopTime:HH\\:mm}) により、{ToLocalText(at)} に停止します。");
    }

    private async Task AutoStopAsync(CancellationTokenSource runCts, AutoStopReason reason)
    {
        try
        {
            switch (reason)
            {
                case AutoStopReason.DataLimit:
                    // 上限は解除しない (累計をリセットするまで、開始できないままにする)
                    _log.Info(
                        $"通信量の累計 ({DataUsage.Format(_dataUsage.Current.TotalBytes)}) が上限 ({_settings.Current.DataLimitMegabytes} MB) に達しました。");
                    await StopCoreAsync(runCts).ConfigureAwait(false);
                    break;

                case AutoStopReason.WeeklySchedule:
                    // 毎週の設定は解除しない (次の週も同じ時刻に止める)
                    var weekly = _settings.Current;
                    _log.Info($"毎週の自動停止の時刻 ({WeeklySchedule.Format(weekly.WeeklyStopDays)} {weekly.WeeklyStopTime:HH\\:mm}) になりました。");
                    await StopCoreAsync(runCts).ConfigureAwait(false);
                    break;

                default:
                    var stopAt = _settings.Current.AutoStopAt;
                    _log.Info($"自動停止日時 ({stopAt?.ToLocalTime():yyyy/MM/dd HH:mm}) になりました。");
                    await StopCoreAsync(runCts).ConfigureAwait(false);

                    // 使い終わった日時を解除し、次回の開始時に再適用されないようにする
                    // (その間に利用者が別の日時を予約していれば、そちらは残す)
                    await _settings.UpdateAsync(
                        current => current.AutoStopAt == stopAt ? current with { AutoStopAt = null } : current)
                        .ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"自動停止の処理に失敗しました: {ex.Message}");
            return;
        }

        try
        {
            AutoStopped?.Invoke(this, reason);
        }
        catch (Exception ex)
        {
            // 通知先 (アプリの終了など) の不具合で、Worker を巻き込まない
            _log.Error($"自動停止の通知先でエラーが発生しました: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// 1 回分の通信をする。通信方式で失敗し、予備の通信方式 (<see cref="AppSettings.FallbackMethod"/>) があれば、
    /// それで 1 回やり直す。どちらも失敗したときだけ失敗として数える。
    /// </summary>
    private async Task ExecuteOnceAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var result = await RunStrategyAsync(settings.Method, cancellationToken).ConfigureAwait(false);
        var fallbackMethod = settings.FallbackMethod is { } method && method != settings.Method ? method : (KeepAliveMethod?)null;

        if (result.Success)
        {
            _log.Info($"成功: {Describe(result)}");
        }
        else if (fallbackMethod is null)
        {
            _log.Warning($"失敗: {Describe(result)}");
        }
        else
        {
            // 予備で成功すれば、回線はつながっている (送信先や方式だけの問題) とみなし、成功として数える
            _log.Warning($"失敗: {Describe(result)}。予備の通信方式でやり直します。");
            var fallback = await RunStrategyAsync(fallbackMethod.Value, cancellationToken).ConfigureAwait(false);
            if (fallback.Success)
            {
                _log.Info($"予備で成功: {Describe(fallback)}");
                result = fallback with { UsedFallback = true };
            }
            else
            {
                _log.Warning($"予備も失敗: {Describe(fallback)}");
                result = result with { Message = $"{result.Message}（予備の {fallback.StrategyName} も失敗: {fallback.Message}）" };
            }
        }

        lock (_gate)
        {
            _statistics = _statistics.Add(result);
        }

        try
        {
            ResultReceived?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            // 通知先 (画面など) の不具合でキープアライブを止めない
            _log.Error($"実行結果の通知先でエラーが発生しました: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>指定した方式で 1 回通信する。Strategy の想定外の例外は、失敗の結果にする。</summary>
    private async Task<KeepAliveResult> RunStrategyAsync(KeepAliveMethod method, CancellationToken cancellationToken)
    {
        IKeepAliveStrategy? strategy = null;
        var timestamp = _timeProvider.GetLocalNow();
        var started = _timeProvider.GetTimestamp();

        try
        {
            strategy = _resolver.Resolve(method);
            return await strategy.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Strategy の想定外例外でループ全体を止めない
            return KeepAliveResult.Failed(
                strategy?.Name ?? method.ToString(), "-", timestamp, _timeProvider.GetElapsedTime(started),
                $"予期しないエラー: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string Describe(KeepAliveResult result) =>
        $"{result.StrategyName} {result.Target} ({result.Elapsed.TotalMilliseconds:F0} ms) {result.Message}".TrimEnd();

    private void RaiseStateChanged()
    {
        try
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Error($"状態変化の通知先でエラーが発生しました: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// 次回実行時刻まで待つ。<see cref="TriggerNow"/> で打ち切られる。待っている間も通信量を定期的に計測する。
    /// 自動停止の条件を満たした場合はその理由を、通信する時になった場合は null を返す。
    /// </summary>
    private async Task<AutoStopReason?> WaitForNextRunAsync(
        DateTimeOffset nextRunAt,
        DateTimeOffset startedAt,
        IDataUsageMeasurement dataUsage,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var now = _timeProvider.GetUtcNow();
            var settings = _settings.Current;
            var stop = GetNextAutoStop(settings, startedAt);
            if (stop is { } due && now >= due.At)
                return due.Reason;

            // 上限に達したら、次回実行を待たずに停止する
            await dataUsage.SampleAsync().ConfigureAwait(false);
            if (IsDataLimitReached(settings))
                return AutoStopReason.DataLimit;

            if (now >= nextRunAt)
            {
                _wake.Reader.TryRead(out _);
                Volatile.Write(ref _runRequested, 0);
                return null;
            }

            // 次回実行・次の通信量の計測・自動停止日時のうち、最も早い時刻に起きる
            var sampleAt = now + _options.DataUsageSampleInterval;
            var wakeAt = sampleAt < nextRunAt ? sampleAt : nextRunAt;
            if (stop is { } next && next.At < wakeAt)
                wakeAt = next.At;

            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var delay = Task.Delay(wakeAt - now, _timeProvider, waitCts.Token);
            var wake = _wake.Reader.WaitToReadAsync(waitCts.Token).AsTask();

            var completed = await Task.WhenAny(delay, wake).ConfigureAwait(false);
            waitCts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            if (completed == delay)
                continue;

            _wake.Reader.TryRead(out _);
            if (Interlocked.Exchange(ref _runRequested, 0) == 1)
                return null;
            // 自動停止の条件だけが変わった場合は通信せず、停止判定と待ち時間を計算し直す
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        var previous = Interlocked.Exchange(ref _lastSettings, settings);
        if (!IsRunning)
            return;

        if (previous.PreventSleepWhileRunning != settings.PreventSleepWhileRunning)
            UpdateSleepInhibition();

        // 数え直しの基準は GetNextWeeklyStop で更新する。ここでは次の停止日時をログに残すだけ
        if (WeeklyStopKey(previous) != WeeklyStopKey(settings))
            LogWeeklyStop(settings, _timeProvider.GetUtcNow());

        if (WithoutNonCommunicationSettings(previous) == WithoutNonCommunicationSettings(settings))
        {
            _wake.Writer.TryWrite(true);
            return;
        }

        _log.Info($"設定変更を反映します ({_resolver.Resolve(settings.Method).Name} / {settings.IntervalSeconds} 秒間隔)。");
        TriggerNow();
    }

    /// <summary>
    /// 自動停止の条件、失敗の通知、スリープの抑止、ネットワークに応じた開始・停止の設定を除いた設定
    /// (通信に関わる設定が変わったかの判定用)。
    /// 自動停止の条件だけが変わったときは、通信せずに停止の判定だけをやり直す。
    /// 通知とネットワークの設定は Worker では使わない。スリープの抑止は、設定の変更時に別に切り替える。
    /// </summary>
    private static AppSettings WithoutNonCommunicationSettings(AppSettings settings) =>
        settings with
        {
            AutoStopAt = null,
            // 予備の方式は失敗したときだけ使うため、変えても今すぐ通信し直す必要はない
            FallbackMethod = null,
            WeeklyStopEnabled = false,
            WeeklyStopTime = default,
            WeeklyStopDays = WeeklyDays.None,
            ExitAppOnAutoStop = false,
            DataLimitMegabytes = 0,
            NotifyOnFailure = false,
            FailureThreshold = 0,
            PreventSleepWhileRunning = false,
            AutoControlByNetwork = false,
            AutoControlNetworkName = string.Empty,
        };

    private void OnSuspending(object? sender, EventArgs e) => _log.Info("OS がスリープに入ります。");

    private void OnResumed(object? sender, EventArgs e)
    {
        if (!IsRunning)
        {
            _log.Info("OS がスリープから復帰しました。");
            return;
        }

        _log.Info($"OS がスリープから復帰しました。{_options.ResumeDelay.TotalSeconds:F0} 秒後に通信を再開します。");
        _ = TriggerAfterDelayAsync(_options.ResumeDelay);
    }

    private async Task TriggerAfterDelayAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _timeProvider).ConfigureAwait(false);
            TriggerNow();
        }
        catch (Exception ex)
        {
            _log.Error($"復帰後の再開処理に失敗しました: {ex.Message}");
        }
    }
}
