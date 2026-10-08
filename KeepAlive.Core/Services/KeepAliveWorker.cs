using System.Threading.Channels;
using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

/// <summary>
/// 設定間隔でキープアライブ通信を繰り返す。
/// 開始直後に 1 回実行し、以降は「前回完了から Interval 後」に実行する。
/// 設定変更・スリープ復帰・<see cref="TriggerNow"/> では待機を打ち切って即時実行する。
/// </summary>
public sealed class KeepAliveWorker : IKeepAliveWorker, IDisposable
{
    private readonly IKeepAliveStrategyResolver _resolver;
    private readonly ISettingsService _settings;
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

    public KeepAliveWorker(
        IKeepAliveStrategyResolver resolver,
        ISettingsService settings,
        ILogService log,
        IPowerEventSource power,
        TimeProvider timeProvider,
        KeepAliveOptions options)
    {
        _resolver = resolver;
        _settings = settings;
        _log = log;
        _power = power;
        _timeProvider = timeProvider;
        _options = options;

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
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_loopTask is not null)
                return;

            while (_wake.Reader.TryRead(out _)) { }
            _runCts = new CancellationTokenSource();
            var token = _runCts.Token;
            // 「開始」ログが初回結果より先に出るよう、ループ本体は開始通知の後に走らせる
            _loopTask = Task.Run(async () =>
            {
                await startSignal.Task.ConfigureAwait(false);
                await RunAsync(token).ConfigureAwait(false);
            }, CancellationToken.None);
        }

        try
        {
            _log.Info($"キープアライブを開始しました ({_resolver.Resolve(s.Method).Name} / {s.IntervalSeconds} 秒間隔)。");
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            startSignal.SetResult();
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
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
        finally
        {
            cts.Dispose();
        }

        _log.Info("キープアライブを停止しました。");
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void TriggerNow() => _wake.Writer.TryWrite(true);

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

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset? lastRunAt = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var settings = _settings.Current;
            var now = _timeProvider.GetUtcNow();

            // 電源イベントを取得できない OS 向けの保険: 想定より大きく時間が飛んでいればスリープ等とみなす
            if (lastRunAt is { } last && now - last > settings.Interval * 2 + settings.Timeout)
                _log.Warning($"前回実行から {(now - last).TotalMinutes:F1} 分経過しています (スリープ／休止からの復帰の可能性)。");
            lastRunAt = now;

            await ExecuteOnceAsync(settings, cancellationToken).ConfigureAwait(false);

            var interval = _settings.Current.Interval;
            lock (_gate)
            {
                if (!cancellationToken.IsCancellationRequested)
                    _nextRunAt = _timeProvider.GetLocalNow() + interval;
            }

            await WaitForNextRunAsync(interval, cancellationToken).ConfigureAwait(false);
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

        ResultReceived?.Invoke(this, result);
    }

    private async Task WaitForNextRunAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = Task.Delay(interval, _timeProvider, waitCts.Token);
        var wake = _wake.Reader.WaitToReadAsync(waitCts.Token).AsTask();

        await Task.WhenAny(delay, wake).ConfigureAwait(false);
        waitCts.Cancel();
        cancellationToken.ThrowIfCancellationRequested();

        _wake.Reader.TryRead(out _);
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (!IsRunning)
            return;

        _log.Info($"設定変更を反映します ({_resolver.Resolve(settings.Method).Name} / {settings.IntervalSeconds} 秒間隔)。");
        TriggerNow();
    }

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
