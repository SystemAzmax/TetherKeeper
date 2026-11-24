using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

/// <summary>
/// 指定したネットワーク (<see cref="AppSettings.AutoControlNetworkName"/>) に接続したらキープアライブを開始し、
/// 切断したら停止する (<see cref="AppSettings.AutoControlByNetwork"/> がオンのとき)。
/// </summary>
/// <remarks>
/// 接続の「変化」で動く。そのため、接続中に利用者が自分で停止した場合は、つなぎ直すまで自動では開始しない。
/// また、指定したネットワーク以外で利用者が自分で開始した場合も、止めない。
/// 接続の変化の通知は 1 回で何度も来るうえ、Wi-Fi は一瞬切れてすぐつながり直すことがあるため、
/// <see cref="KeepAliveOptions.NetworkChangeDelay"/> だけ落ち着くのを待ってから判定する。
/// </remarks>
public sealed class NetworkAutoControl : IDisposable
{
    private readonly INetworkMonitor _monitor;
    private readonly IKeepAliveWorker _worker;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;
    private readonly TimeProvider _timeProvider;
    private readonly KeepAliveOptions _options;

    private readonly object _gate = new();
    private readonly SemaphoreSlim _evaluating = new(1, 1);
    private ITimer? _delayTimer;
    private bool _activated;
    private bool _disposed;

    /// <summary>前回の判定で、指定したネットワークにつながっていたか。null はまだ判定していない (または設定が変わった)。</summary>
    private bool? _wasConnected;

    private AppSettings _lastSettings;

    public NetworkAutoControl(
        INetworkMonitor monitor,
        IKeepAliveWorker worker,
        ISettingsService settings,
        ILogService log,
        TimeProvider timeProvider,
        KeepAliveOptions options)
    {
        _monitor = monitor;
        _worker = worker;
        _settings = settings;
        _log = log;
        _timeProvider = timeProvider;
        _options = options;
        _lastSettings = settings.Current;

        _monitor.NetworksChanged += OnNetworksChanged;
        _settings.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>この OS で使えるか。</summary>
    public bool IsSupported => _monitor.IsSupported;

    /// <summary>
    /// 指定したネットワークから切断したため、キープアライブを停止した。引数はネットワークの名前。
    /// どのスレッドから発火するかは決まっていない。
    /// </summary>
    public event EventHandler<string>? StoppedByDisconnection;

    /// <summary>
    /// 設定を読み込んだあとに 1 回だけ呼ぶ。今つながっていれば開始し、以降は接続の変化を見る。
    /// 呼ぶまでは、接続が変わっても何もしない (読み込み前の既定の設定で動かないようにするため)。
    /// </summary>
    public Task ActivateAsync()
    {
        lock (_gate)
        {
            if (_activated || _disposed)
                return Task.CompletedTask;
            _activated = true;
            _lastSettings = _settings.Current;
        }

        return EvaluateAsync();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _delayTimer?.Dispose();
            _delayTimer = null;
        }

        _monitor.NetworksChanged -= OnNetworksChanged;
        _settings.SettingsChanged -= OnSettingsChanged;
    }

    private void OnNetworksChanged(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (!_activated || _disposed || !_settings.Current.AutoControlByNetwork)
                return;

            // 通知が続く間は待ち直し、最後の通知から一定時間たってから 1 回だけ判定する
            _delayTimer?.Dispose();
            _delayTimer = _timeProvider.CreateTimer(
                _ => _ = EvaluateAsync(), null, _options.NetworkChangeDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        lock (_gate)
        {
            var previous = _lastSettings;
            _lastSettings = settings;
            if (!_activated || _disposed)
                return;
            if (previous.AutoControlByNetwork == settings.AutoControlByNetwork
                && previous.AutoControlNetworkName == settings.AutoControlNetworkName)
                return;

            // 対象が変わったら、前回の判定は使えない。オンにしたときに接続中なら、すぐに開始する
            _wasConnected = null;
        }

        _ = EvaluateAsync();
    }

    /// <summary>指定したネットワークにつながっているかを調べ、前回から変わっていれば開始・停止する。</summary>
    private async Task EvaluateAsync()
    {
        await _evaluating.WaitAsync().ConfigureAwait(false);
        try
        {
            var settings = _settings.Current;
            if (!settings.AutoControlByNetwork || !_monitor.IsSupported)
            {
                lock (_gate) _wasConnected = null;
                return;
            }

            var name = settings.AutoControlNetworkName;
            var isConnected = _monitor.GetConnectedNetworks().Any(n => string.Equals(n.Name, name, StringComparison.Ordinal));

            bool? wasConnected;
            lock (_gate)
            {
                if (_disposed)
                    return;
                wasConnected = _wasConnected;
                _wasConnected = isConnected;
            }

            if (isConnected && wasConnected != true)
            {
                if (_worker.IsRunning)
                    return;

                _log.Info($"「{name}」に接続しているため、キープアライブを開始します。");
                _worker.Start();
            }
            else if (!isConnected && wasConnected == true && _worker.IsRunning)
            {
                _log.Info($"「{name}」から切断したため、キープアライブを停止します。");
                await _worker.StopAsync().ConfigureAwait(false);
                StoppedByDisconnection?.Invoke(this, name);
            }
        }
        catch (Exception ex)
        {
            // 接続の取得や通知先の失敗で、アプリを落とさない
            _log.Warning($"ネットワークに応じた自動開始・停止の処理に失敗しました: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _evaluating.Release();
        }
    }
}
