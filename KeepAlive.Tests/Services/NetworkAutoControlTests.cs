using KeepAlive.Core.Configuration;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace KeepAlive.Tests.Services;

public sealed class NetworkAutoControlTests : IDisposable
{
    private const string Tethering = "iPhone";
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _time = new();
    private readonly FakeNetworkMonitor _monitor = new();
    private readonly FakeKeepAliveWorker _worker = new();
    private readonly InMemorySettingsStore _store = new();
    private readonly LogService _log;
    private readonly SettingsService _settings;
    private readonly NetworkAutoControl _control;
    private readonly List<string> _stoppedBy = [];

    public NetworkAutoControlTests()
    {
        _log = TestHelpers.CreateLogService(_time);
        _store.Stored = new AppSettings { AutoControlByNetwork = true, AutoControlNetworkName = Tethering };
        _settings = new SettingsService(_store, _log);
        _settings.LoadAsync().GetAwaiter().GetResult();
        _control = new NetworkAutoControl(
            _monitor, _worker, _settings, _log, _time, new KeepAliveOptions { NetworkChangeDelay = Delay });
        _control.StoppedByDisconnection += (_, name) => { lock (_stoppedBy) _stoppedBy.Add(name); };
    }

    public void Dispose() => _control.Dispose();

    /// <summary>接続を変え、判定までの待ち時間を進める。</summary>
    private void ChangeNetworks(params string[] names)
    {
        _monitor.SetConnected(names);
        _time.Advance(Delay);
    }

    [Fact]
    public async Task Activate_StartsWhenAlreadyConnected()
    {
        _monitor.SetConnected(Tethering);

        await _control.ActivateAsync();

        Assert.Equal(1, _worker.StartCount);
        Assert.Contains(_log.GetEntries(), e => e.Message.Contains("「iPhone」に接続しているため"));
    }

    [Fact]
    public async Task Activate_DoesNothing_WhenNotConnected()
    {
        _monitor.SetConnected("自宅の Wi-Fi");

        await _control.ActivateAsync();

        Assert.Equal(0, _worker.StartCount);
    }

    [Fact]
    public void NetworkChanges_AreIgnoredBeforeActivate()
    {
        // 設定の読み込み前に、既定の設定で動かないようにする
        ChangeNetworks(Tethering);

        Assert.Equal(0, _worker.StartCount);
    }

    [Fact]
    public async Task Connecting_StartsAfterDelay_AndDisconnecting_Stops()
    {
        await _control.ActivateAsync();

        _monitor.SetConnected(Tethering);
        _time.Advance(Delay - TimeSpan.FromSeconds(1));
        Assert.Equal(0, _worker.StartCount);

        _time.Advance(TimeSpan.FromSeconds(1));
        await TestHelpers.WaitUntilAsync(() => _worker.IsRunning);

        ChangeNetworks("自宅の Wi-Fi");
        await TestHelpers.WaitUntilAsync(() => !_worker.IsRunning);
        await TestHelpers.WaitUntilAsync(() => { lock (_stoppedBy) return _stoppedBy.Count == 1; });
        Assert.Equal(Tethering, _stoppedBy[0]);
    }

    [Fact]
    public async Task ShortDisconnection_WithinDelay_IsIgnored()
    {
        _monitor.SetConnected(Tethering);
        await _control.ActivateAsync();
        Assert.True(_worker.IsRunning);

        // 一瞬切れてすぐつながり直した場合は、待ち時間が延びて、最後の状態 (接続中) だけで判定する
        _monitor.SetConnected();
        _time.Advance(TimeSpan.FromSeconds(2));
        _monitor.SetConnected(Tethering);
        _time.Advance(Delay);
        await Task.Delay(100);

        Assert.True(_worker.IsRunning);
        Assert.Equal(0, _worker.StopCount);
    }

    [Fact]
    public async Task ManualStopWhileConnected_IsNotUndone_UntilReconnected()
    {
        _monitor.SetConnected(Tethering);
        await _control.ActivateAsync();
        await _worker.StopAsync();

        // 接続したまま、ほかの変化が通知されても開始し直さない
        ChangeNetworks(Tethering, "イーサネット");
        await Task.Delay(100);
        Assert.Equal(1, _worker.StartCount);

        // つなぎ直したら開始する
        ChangeNetworks();
        ChangeNetworks(Tethering);
        await TestHelpers.WaitUntilAsync(() => _worker.StartCount == 2);
    }

    [Fact]
    public async Task ManualStartOnOtherNetwork_IsNotStopped()
    {
        _monitor.SetConnected("自宅の Wi-Fi");
        await _control.ActivateAsync();
        _worker.Start();

        ChangeNetworks("会社の Wi-Fi");
        await Task.Delay(100);

        Assert.True(_worker.IsRunning);
        Assert.Empty(_stoppedBy);
    }

    [Fact]
    public async Task NetworkName_IsCaseSensitive()
    {
        _monitor.SetConnected("IPHONE");

        await _control.ActivateAsync();

        Assert.Equal(0, _worker.StartCount);
    }

    [Fact]
    public async Task Disabled_DoesNothing()
    {
        await _settings.SaveAsync(_settings.Current with { AutoControlByNetwork = false });
        _monitor.SetConnected(Tethering);

        await _control.ActivateAsync();
        ChangeNetworks();
        ChangeNetworks(Tethering);
        await Task.Delay(100);

        Assert.Equal(0, _worker.StartCount);
    }

    [Fact]
    public async Task EnablingWhileConnected_StartsImmediately()
    {
        await _settings.SaveAsync(_settings.Current with { AutoControlByNetwork = false });
        _monitor.SetConnected(Tethering);
        await _control.ActivateAsync();

        await _settings.SaveAsync(_settings.Current with { AutoControlByNetwork = true });

        await TestHelpers.WaitUntilAsync(() => _worker.StartCount == 1);
    }

    [Fact]
    public async Task ChangingTargetNetwork_DoesNotStopCurrentRun()
    {
        _monitor.SetConnected(Tethering);
        await _control.ActivateAsync();

        // 対象を変えただけでは、前の対象からの「切断」とはみなさない
        await _settings.SaveAsync(_settings.Current with { AutoControlNetworkName = "Android" });
        await Task.Delay(100);

        Assert.True(_worker.IsRunning);
        Assert.Equal(0, _worker.StopCount);
    }

    [Fact]
    public async Task MonitorFailure_IsLogged_AndDoesNotThrow()
    {
        var control = new NetworkAutoControl(
            new ThrowingNetworkMonitor(), _worker, _settings, _log, _time, new KeepAliveOptions());

        await control.ActivateAsync();

        Assert.Contains(_log.GetEntries(), e => e.Level == LogLevelKind.Warning && e.Message.Contains("network boom"));
        control.Dispose();
    }

    private sealed class ThrowingNetworkMonitor : KeepAlive.Core.Interfaces.INetworkMonitor
    {
        public bool IsSupported => true;

#pragma warning disable CS0067
        public event EventHandler? NetworksChanged;
#pragma warning restore CS0067

        public IReadOnlyList<ConnectedNetwork> GetConnectedNetworks() => throw new InvalidOperationException("network boom");
    }
}
