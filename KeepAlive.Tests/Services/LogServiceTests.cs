using KeepAlive.Core.Interfaces;

namespace KeepAlive.Tests.Services;

public sealed class LogServiceTests
{
    [Fact]
    public void Log_DoesNotThrow_WhenSubscriberThrows()
    {
        var log = TestHelpers.CreateLogService();
        log.EntryAdded += (_, _) => throw new InvalidOperationException("subscriber boom");

        log.Info("hello");

        Assert.Equal("hello", Assert.Single(log.GetEntries()).Message);
    }
}
