using Foundation;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Services;
using ServiceManagement;

namespace TetherKeeper.Mac.Services;

/// <summary>
/// Mac へのログイン時の自動起動。アプリ自身をログイン項目に登録する (SMAppService、macOS 13 以降)。
/// 登録した状態は、システム設定の「一般」→「ログイン項目」で利用者が切り替えられる。
/// </summary>
public sealed class MacAutoStartService : IAutoStartService
{
    public bool IsSupported => true;

    public Task<bool> IsEnabledAsync() =>
        Task.FromResult(SMAppService.MainApp.Status == SMAppServiceStatus.Enabled);

    public Task SetEnabledAsync(bool enabled)
    {
        var service = SMAppService.MainApp;
        NSError? error;
        var succeeded = enabled ? service.Register(out error) : service.Unregister(out error);

        if (enabled)
        {
            switch (service.Status)
            {
                case SMAppServiceStatus.Enabled:
                    return Task.CompletedTask;

                case SMAppServiceStatus.RequiresApproval:
                    // 利用者がログイン項目でオフにしている。アプリからはオンにできないため、設定の画面を開いて知らせる
                    SMAppService.OpenSystemSettingsLoginItems();
                    throw new InvalidOperationException(
                        Text.Format(MacStrings.AutoStart_RequiresApproval, AppDisplayName.GetCurrent()));

                case SMAppServiceStatus.NotFound:
                    throw new InvalidOperationException(
                        Text.Format(MacStrings.AutoStart_NotFound, AppDisplayName.GetCurrent()));
            }
        }
        else if (service.Status is SMAppServiceStatus.NotRegistered or SMAppServiceStatus.NotFound)
        {
            return Task.CompletedTask;
        }

        if (!succeeded)
            throw new InvalidOperationException(error?.LocalizedDescription ?? service.Status.ToString());

        return Task.CompletedTask;
    }
}
