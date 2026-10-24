using KeepAlive.Core.Configuration;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KeepAlive.Tests.Strategies;

public class KeepAliveStrategyResolverTests
{
    [Fact]
    public void Resolve_ReturnsStrategyForMethod()
    {
        var ping = new FakeStrategy(KeepAliveMethod.Ping, TimeProvider.System);
        var tcp = new FakeStrategy(KeepAliveMethod.TcpConnect, TimeProvider.System);
        var resolver = new KeepAliveStrategyResolver([ping, tcp]);

        Assert.Same(tcp, resolver.Resolve(KeepAliveMethod.TcpConnect));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(KeepAliveMethod.HttpHead));
    }

    [Fact]
    public void AddKeepAliveCore_RegistersAStrategyForEveryMethod()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<ISettingsStore, InMemorySettingsStore>()
            .AddSingleton<IDataUsageStore, InMemoryDataUsageStore>()
            .AddSingleton<IDataUsageMeter, FakeDataUsageMeter>()
            .AddSingleton<IPowerEventSource, FakePowerEventSource>()
            .AddKeepAliveCore()
            .BuildServiceProvider();

        var resolver = provider.GetRequiredService<IKeepAliveStrategyResolver>();

        foreach (var method in Enum.GetValues<KeepAliveMethod>())
            Assert.Equal(method, resolver.Resolve(method).Method);
        Assert.NotNull(provider.GetRequiredService<IKeepAliveWorker>());
    }
}
