using KeepAlive.Core.Interfaces;

namespace KeepAlive.Infrastructure.AutoStart;

/// <summary>
/// 自動起動に未対応のプラットフォーム用 (Windows 以外)。
/// </summary>
public sealed class UnsupportedAutoStartService : IAutoStartService
{
    public bool IsSupported => false;

    public Task<bool> IsEnabledAsync() => Task.FromResult(false);

    public Task SetEnabledAsync(bool enabled) => enabled
        ? Task.FromException(new PlatformNotSupportedException("このプラットフォームでは自動起動を設定できません。"))
        : Task.CompletedTask;
}
