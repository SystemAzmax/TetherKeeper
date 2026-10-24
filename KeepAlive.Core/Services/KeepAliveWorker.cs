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
    private readonly TimeProvider _timeProvider;
    private readonly KeepAliveOptions _options;

    private readonly object _gate = new();
    private readonly Channel<bool> _wake =
        Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private CancellationTokenSource? _runCts;
    private Task? _loopTask;
    private KeepAliveStatistics _statistics = KeepAliveStatistics.Empty;
    private DateTimeOffset? _nextRunAt;
    private AppSettings _lastSettings;

    /// <summary>待機の打ち切りが通信の要求か (1)、自動停止の条件の再計算のみか (0)。</summary>
    private int _runRequested;

    private enum AutoStopReason
    {
        /// <summary>自動停止日時 (<see cref="AppSettings.AutoStopAt"/>) になった。</summary>
        ScheduledTime,

        /// <summary>通信量の累計が上限 (<see cref="AppSettings.DataLimitMegabytes"/>) に達した。</summary>
        DataLimit,
    }

    public KeepAliveWorker(
        IKeepAliveStrategyResolver resolver,
        ISettingsService settings,
        IDataUsageTracker dataUsage,
        ILogService log,
        IPowerEventSource power,
        TimeProvider timeProvider,
        KeepAliveOptions options)
    {
        _resolver = resolver;
        _settings = settings;
        _dataUsage = dataUsage;
        _log = log;
        _power = power;
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

            if (s.DataLimitBytes is not null)
                _log.Info($"通信量の累計が {s.DataLimitMegabytes} MB に達したら自動停止します (現在 {DataUsage.Format(_dataUsage.Current.TotalBytes)})。");

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
        RaiseStateChanged();
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
                if (GetActiveAutoStopAt(settings, startedAt) is { } stopAt && now >= stopAt)
                    return AutoStopReason.ScheduledTime;
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

    private async Task AutoStopAsync(CancellationTokenSource runCts, AutoStopReason reason)
    {
        try
        {
            if (reason == AutoStopReason.DataLimit)
            {
                // 上限は解除しない (累計をリセットするまで、開始できないままにする)
                _log.Info(
                    $"通信量の累計 ({DataUsage.Format(_dataUsage.Current.TotalBytes)}) が上限 ({_settings.Current.DataLimitMegabytes} MB) に達しました。");
                await StopCoreAsync(runCts).ConfigureAwait(false);
                return;
            }

            var stopAt = _settings.Current.AutoStopAt;
            _log.Info($"自動停止日時 ({stopAt?.ToLocalTime():yyyy/MM/dd HH:mm}) になりました。");
            await StopCoreAsync(runCts).ConfigureAwait(false);

            // 使い終わった日時を解除し、次回の開始時に再適用されないようにする
            // (その間に利用者が別の日時を予約していれば、そちらは残す)
            await _settings.UpdateAsync(
                current => current.AutoStopAt == stopAt ? current with { AutoStopAt = null } : current)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error($"自動停止の処理に失敗しました: {ex.Message}");
        }
    }

    private async Task ExecuteOnceAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        IKeepAliveStrategy? strategy = null;
        var timestamp = _timeProvider.GetLocalNow();
        var started = _timeProvider.GetTimestamp();
        KeepAliveResult result;

        try
        {
            strategy = _resolver.Resolve(settings.Method);
            result = await strategy.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Strategy の想定外例外でループ全体を止めない
            result = KeepAliveResult.Failed(
                strategy?.Name ?? settings.Method.ToString(), "-", timestamp, _timeProvider.GetElapsedTime(started),
                $"予期しないエラー: {ex.GetType().Name}: {ex.Message}");
        }

        lock (_gate)
        {
            _statistics = _statistics.Add(result);
        }

        var detail = $"{result.StrategyName} {result.Target} ({result.Elapsed.TotalMilliseconds:F0} ms)";
        if (result.Success)
            _log.Info($"成功: {detail} {result.Message}".TrimEnd());
        else
            _log.Warning($"失敗: {detail} {result.Message}".TrimEnd());

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
            var stopAt = GetActiveAutoStopAt(settings, startedAt);
            if (now >= stopAt)
                return AutoStopReason.ScheduledTime;

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
            if (stopAt < wakeAt)
                wakeAt = stopAt.Value;

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

        if (WithoutAutoStopConditions(previous) == WithoutAutoStopConditions(settings))
        {
            _wake.Writer.TryWrite(true);
            return;
        }

        _log.Info($"設定変更を反映します ({_resolver.Resolve(settings.Method).Name} / {settings.IntervalSeconds} 秒間隔)。");
        TriggerNow();
    }

    /// <summary>自動停止の条件を除いた設定 (通信に関わる設定が変わったかの判定用)。</summary>
    private static AppSettings WithoutAutoStopConditions(AppSettings settings) =>
        settings with { AutoStopAt = null, DataLimitMegabytes = 0 };

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
