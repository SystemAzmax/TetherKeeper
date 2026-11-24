using KeepAlive.Core.Interfaces;

namespace KeepAlive.Infrastructure.Power;

/// <summary>スリープを抑止できないプラットフォーム用。何もしない。</summary>
public sealed class UnsupportedSleepInhibitor : ISleepInhibitor
{
    public bool IsSupported => false;

    public void SetEnabled(bool enabled)
    {
    }
}
