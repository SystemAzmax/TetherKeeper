namespace KeepAlive.Core.Interfaces;

/// <summary>
/// OS のスリープ／復帰通知 (OS 依存。実装は Infrastructure)。
/// </summary>
public interface IPowerEventSource
{
    event EventHandler? Suspending;

    event EventHandler? Resumed;
}
