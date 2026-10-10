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
