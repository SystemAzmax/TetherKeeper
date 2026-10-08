using KeepAlive.Core.Interfaces;
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
                throw new InvalidOperationException("組織のポリシーで自動起動が有効にされているため、無効にできません。");

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
                    "タスクマネージャーの「スタートアップ アプリ」で TetherKeeper が無効にされています。そちらで有効にしてください。");
            case StartupTaskState.DisabledByPolicy:
                throw new InvalidOperationException("組織のポリシーで自動起動が禁止されています。");
            default:
                throw new InvalidOperationException($"自動起動を有効にできませんでした (状態: {state})。");
        }
    }
}
