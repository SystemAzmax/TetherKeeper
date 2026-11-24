using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Services;
using Windows.ApplicationModel;

namespace TetherKeeper.WinUI;

/// <summary>
/// MSIX パッケージ版のログイン時自動起動。
/// パッケージアプリはレジストリの Run キーを使えないため、Package.appxmanifest に宣言した
/// StartupTask (windows.startupTask 拡張) を有効／無効にする。
/// ユーザーはタスクマネージャーの「スタートアップ アプリ」からも切り替えられ、
/// そこで無効にされた場合はアプリ側から再度有効にすることはできない (Windows の仕様)。
/// </summary>
internal sealed class StartupTaskAutoStartService : IAutoStartService
{
    /// <summary>Package.appxmanifest の uap5:StartupTask の TaskId と一致させること。</summary>
    public const string TaskId = "TetherKeeperStartup";

    public bool IsSupported => true;

    public async Task<bool> IsEnabledAsync()
    {
        var task = await StartupTask.GetAsync(TaskId);
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        var task = await StartupTask.GetAsync(TaskId);

        if (!enabled)
        {
            if (task.State == StartupTaskState.EnabledByPolicy)
                throw new InvalidOperationException(AppStrings.StartupTask_EnabledByPolicy);

            task.Disable();
            return;
        }

        var state = task.State == StartupTaskState.Disabled
            ? await task.RequestEnableAsync()
            : task.State;

        switch (state)
        {
            case StartupTaskState.Enabled:
            case StartupTaskState.EnabledByPolicy:
                return;
            case StartupTaskState.DisabledByUser:
                throw new InvalidOperationException(
                    // タスクマネージャーには、パッケージのリソースの表示名 (表示言語の名前) が出る
                    Text.Format(AppStrings.StartupTask_DisabledByUser, AppDisplayName.GetCurrent()));
            case StartupTaskState.DisabledByPolicy:
                throw new InvalidOperationException(AppStrings.StartupTask_DisabledByPolicy);
            default:
                throw new InvalidOperationException(Text.Format(AppStrings.StartupTask_EnableFailed, state));
        }
    }
}
