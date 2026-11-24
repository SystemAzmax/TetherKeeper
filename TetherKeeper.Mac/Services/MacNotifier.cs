using Foundation;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Services;
using UserNotifications;

namespace TetherKeeper.Mac.Services;

/// <summary>
/// 通信の失敗が続いたときと、復旧したときの通知 (macOS の通知センター)。
/// 初めて通知を使うときに、macOS が利用者に許可を求める。
/// </summary>
public sealed class MacNotifier : UNUserNotificationCenterDelegate
{
    private readonly ILogService _log;
    private bool _authorizationRequested;

    public MacNotifier(ILogService log)
    {
        _log = log;
        // アプリが前面にあるときも通知を出す (設定のウィンドウを開いている間など)
        UNUserNotificationCenter.Current.Delegate = this;
    }

    /// <summary>通知の許可を求める (許可・拒否が決まっていれば、macOS は何も聞かない)。</summary>
    public void RequestAuthorization()
    {
        if (_authorizationRequested)
            return;
        _authorizationRequested = true;

        UNUserNotificationCenter.Current.RequestAuthorization(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound,
            (granted, error) =>
            {
                if (!granted)
                {
                    _log.Warning(Text.Format(MacStrings.Notification_PermissionDenied, AppDisplayName.GetCurrent())
                        + (error is null ? string.Empty : $" ({error.LocalizedDescription})"));
                }
            });
    }

    public void Show(string title, string message)
    {
        RequestAuthorization();

        var content = new UNMutableNotificationContent
        {
            Title = title,
            Body = message,
            Sound = UNNotificationSound.Default,
        };
        var request = UNNotificationRequest.FromIdentifier(Guid.NewGuid().ToString(), content, null);
        UNUserNotificationCenter.Current.AddNotificationRequest(request, error =>
        {
            if (error is not null)
                System.Diagnostics.Debug.WriteLine($"MacNotifier: 通知を出せませんでした: {error.LocalizedDescription}");
        });
    }

    public override void WillPresentNotification(
        UNUserNotificationCenter center,
        UNNotification notification,
        Action<UNNotificationPresentationOptions> completionHandler) =>
        completionHandler(UNNotificationPresentationOptions.Banner
            | UNNotificationPresentationOptions.List
            | UNNotificationPresentationOptions.Sound);
}
