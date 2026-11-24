using System.Collections.Concurrent;
using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace KeepAlive.Tests;

internal sealed class InMemorySettingsStore : ISettingsStore
{
    public AppSettings? Stored { get; set; }

    public Exception? LoadException { get; set; }

    public Exception? SaveException { get; set; }

    public int SaveCount { get; private set; }

    /// <summary>設定すると、保存の前にこのタスクの完了を待つ (保存が重なる状況の再現用)。</summary>
    public Task? SaveGate { get; set; }

    public Task<AppSettings?> LoadAsync(CancellationToken cancellationToken = default) =>
        LoadException is null ? Task.FromResult(Stored) : Task.FromException<AppSettings?>(LoadException);

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (SaveGate is { } gate)
            await gate;
        if (SaveException is not null)
            throw SaveException;

        SaveCount++;
        Stored = settings;
    }
}

internal sealed class InMemoryDataUsageStore : IDataUsageStore
{
    private DataUsage? _stored;

    public DataUsage? Stored
    {
        get => Volatile.Read(ref _stored);
        set => Volatile.Write(ref _stored, value);
    }

    public int SaveCount { get; private set; }

    public Task<DataUsage?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Stored);

    public Task SaveAsync(DataUsage usage, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        Stored = usage;
        return Task.CompletedTask;
    }
}

internal sealed class FakeDataUsageMeter : IDataUsageMeter
{
    private DataUsageReading? _reading = new("fake", "FakeNetwork", 0);

    /// <summary>次回以降の読み取りで返す値。null はネットワーク無し。</summary>
    public DataUsageReading? Reading
    {
        get => Volatile.Read(ref _reading);
        set => Volatile.Write(ref _reading, value);
    }

    /// <summary>設定すると読み取り時に送出する。</summary>
    public Exception? ThrowOnRead { get; set; }

    /// <summary>今のアダプターの送受信量を増やす (通信が発生した状況の再現用)。</summary>
    public void AddBytes(long bytes) => Reading = Reading! with { TotalBytes = Reading.TotalBytes + bytes };

    public DataUsageReading? Read() => ThrowOnRead is { } ex ? throw ex : Reading;
}

internal sealed class FakeStrategy : IKeepAliveStrategy
{
    private readonly TimeProvider _timeProvider;

    public FakeStrategy(KeepAliveMethod method, TimeProvider timeProvider)
    {
        Method = method;
        _timeProvider = timeProvider;
    }

    public string Name => $"Fake{Method}";

    public KeepAliveMethod Method { get; }

    /// <summary>次回以降の実行で返す成否。</summary>
    public bool NextSuccess { get; set; } = true;

    /// <summary>設定すると実行時に送出する。</summary>
    public Exception? ThrowOnExecute { get; set; }

    public ConcurrentQueue<DateTimeOffset> Executions { get; } = new();

    public int ExecutionCount => Executions.Count;

    public Task<KeepAliveResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        Executions.Enqueue(now);

        if (ThrowOnExecute is not null)
            throw ThrowOnExecute;

        return Task.FromResult(NextSuccess
            ? KeepAliveResult.Succeeded(Name, "fake", now, TimeSpan.Zero)
            : KeepAliveResult.Failed(Name, "fake", now, TimeSpan.Zero, "fake failure"));
    }
}

internal sealed class FakePowerEventSource : IPowerEventSource
{
    public event EventHandler? Suspending;

    public event EventHandler? Resumed;

    public void RaiseSuspending() => Suspending?.Invoke(this, EventArgs.Empty);

    public void RaiseResumed() => Resumed?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeNetworkMonitor : INetworkMonitor
{
    private volatile IReadOnlyList<ConnectedNetwork> _networks = [];

    public bool IsSupported { get; init; } = true;

    public event EventHandler? NetworksChanged;

    public IReadOnlyList<ConnectedNetwork> GetConnectedNetworks() => _networks;

    /// <summary>接続中のネットワークを変え、変化を通知する。</summary>
    public void SetConnected(params string[] names)
    {
        _networks = names.Select(n => new ConnectedNetwork(n, NetworkKind.WiFi)).ToList();
        NetworksChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>開始・停止の呼び出しだけを記録する IKeepAliveWorker。</summary>
internal sealed class FakeKeepAliveWorker : IKeepAliveWorker
{
    private int _startCount;
    private int _stopCount;
    private volatile bool _isRunning;

    public bool IsRunning => _isRunning;

    public int StartCount => Volatile.Read(ref _startCount);

    public int StopCount => Volatile.Read(ref _stopCount);

    public KeepAliveStatistics Statistics => KeepAliveStatistics.Empty;

    public DateTimeOffset? NextRunAt => null;

    public event EventHandler? StateChanged;

#pragma warning disable CS0067 // 発火しないイベント
    public event EventHandler<KeepAliveResult>? ResultReceived;
#pragma warning restore CS0067

    public void Start()
    {
        Interlocked.Increment(ref _startCount);
        _isRunning = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task StopAsync()
    {
        Interlocked.Increment(ref _stopCount);
        _isRunning = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void TriggerNow()
    {
    }

    public void ResetStatistics()
    {
    }
}

internal sealed class FakeSleepInhibitor : ISleepInhibitor
{
    private int _changeCount;

    public bool IsSupported { get; init; } = true;

    public bool IsEnabled { get; private set; }

    /// <summary>有効・無効を切り替えた回数。</summary>
    public int ChangeCount => Volatile.Read(ref _changeCount);

    /// <summary>true にすると、SetEnabled で例外を投げる。</summary>
    public bool Throw { get; set; }

    public void SetEnabled(bool enabled)
    {
        if (Throw)
            throw new InvalidOperationException("sleep boom");

        IsEnabled = enabled;
        Interlocked.Increment(ref _changeCount);
    }
}

/// <summary>固定の設定を返すだけの ISettingsService (Strategy テスト用)。</summary>
internal sealed class StaticSettingsService(AppSettings settings) : ISettingsService
{
    public AppSettings Current { get; set; } = settings;

#pragma warning disable CS0067
    public event EventHandler<AppSettings>? SettingsChanged;
#pragma warning restore CS0067

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

    public Task<IReadOnlyList<string>> SaveAsync(AppSettings s, CancellationToken cancellationToken = default)
    {
        Current = s;
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    public Task<IReadOnlyList<string>> UpdateAsync(
        Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default) =>
        SaveAsync(update(Current), cancellationToken);
}

/// <summary><see cref="Throw"/> を true にすると Current の取得で例外を投げる ISettingsService (異常系テスト用)。</summary>
internal sealed class FaultySettingsService(AppSettings settings) : ISettingsService
{
    public volatile bool Throw;

    public AppSettings Current => Throw ? throw new InvalidOperationException("settings boom") : settings;

#pragma warning disable CS0067
    public event EventHandler<AppSettings>? SettingsChanged;
#pragma warning restore CS0067

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

    public Task<IReadOnlyList<string>> SaveAsync(AppSettings s, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> UpdateAsync(
        Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}

internal static class TestHelpers
{
    public static LogService CreateLogService(TimeProvider? timeProvider = null) =>
        new(new KeepAliveOptions(), timeProvider ?? TimeProvider.System, NullLogger<LogService>.Instance, []);

    /// <summary>
    /// 条件が満たされるまで待つ。<paramref name="advance"/> を渡した場合は待機ごとに仮想時刻を進める。
    /// </summary>
    public static async Task WaitUntilAsync(Func<bool> condition, Action? advance = null, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("条件が時間内に満たされませんでした。");

            advance?.Invoke();
            await Task.Delay(10);
        }
    }
}
