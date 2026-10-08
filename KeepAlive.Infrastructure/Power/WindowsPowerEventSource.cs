using System.Runtime.Versioning;
using KeepAlive.Core.Interfaces;
using Microsoft.Win32;

namespace KeepAlive.Infrastructure.Power;

/// <summary>
/// <see cref="SystemEvents.PowerModeChanged"/> (WM_POWERBROADCAST) によるスリープ／復帰通知。
/// SystemEvents は専用の隠しウィンドウスレッドで通知を受けるため、イベントは UI スレッド以外から発火する。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsPowerEventSource : IPowerEventSource, IDisposable
{
    public WindowsPowerEventSource()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public event EventHandler? Suspending;

    public event EventHandler? Resumed;

    public void Dispose() => SystemEvents.PowerModeChanged -= OnPowerModeChanged;

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                Suspending?.Invoke(this, EventArgs.Empty);
                break;
            case PowerModes.Resume:
                Resumed?.Invoke(this, EventArgs.Empty);
                break;
        }
    }
}
