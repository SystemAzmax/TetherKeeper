using KeepAlive.Infrastructure.Power;

namespace KeepAlive.Tests.Power;

public class WindowsSleepInhibitorTests
{
    [Fact]
    public void SetEnabled_CanBeToggledRepeatedly_AndIsIdempotent()
    {
        // Windows の API を実際に呼ぶため、Windows でだけ確かめる
        if (!OperatingSystem.IsWindows())
            return;

        using var inhibitor = new WindowsSleepInhibitor("TetherKeeper test");

        Assert.True(inhibitor.IsSupported);
        inhibitor.SetEnabled(true);
        inhibitor.SetEnabled(true);
        inhibitor.SetEnabled(false);
        inhibitor.SetEnabled(false);
        inhibitor.SetEnabled(true);
        inhibitor.SetEnabled(false);
    }

    [Fact]
    public void SetEnabled_AfterDispose_Throws()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var inhibitor = new WindowsSleepInhibitor("TetherKeeper test");
        inhibitor.SetEnabled(true);
        inhibitor.Dispose();
        inhibitor.Dispose();

        Assert.Throws<ObjectDisposedException>(() => inhibitor.SetEnabled(false));
    }

    [Fact]
    public void UnsupportedSleepInhibitor_DoesNothing()
    {
        var inhibitor = new UnsupportedSleepInhibitor();

        Assert.False(inhibitor.IsSupported);
        inhibitor.SetEnabled(true);
        inhibitor.SetEnabled(false);
    }
}
