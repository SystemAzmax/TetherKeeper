namespace TetherKeeper.WinUI;

/// <summary>
/// 多重起動の防止。トレイ常駐中に exe を再度起動した場合は、新しいプロセスは終了し、
/// 既存のプロセスに「ウィンドウを表示して」と合図を送る。
/// </summary>
internal static class SingleInstance
{
    // Local\ はログオンセッション単位 (同じ PC の別ユーザーとは干渉しない)
    private const string MutexName = @"Local\TetherKeeper.SingleInstance";
    private const string ActivateEventName = @"Local\TetherKeeper.Activate";

    private static Mutex? _mutex;
    private static EventWaitHandle? _activateEvent;
    private static RegisteredWaitHandle? _registeredWait;

    /// <summary>最初のインスタンスであれば true。</summary>
    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        return true;
    }

    /// <summary>既に起動しているインスタンスにウィンドウ表示を依頼する。</summary>
    public static void SignalExistingInstance()
    {
        if (EventWaitHandle.TryOpenExisting(ActivateEventName, out var activateEvent))
        {
            using (activateEvent)
                activateEvent.Set();
        }
    }

    /// <summary>
    /// 他のプロセスから表示依頼が来たときの処理を登録する。
    /// コールバックはスレッドプールで呼ばれるため、UI 操作は呼び出し側で UI スレッドに移すこと。
    /// </summary>
    public static void ListenForActivation(Action onActivate)
    {
        if (_activateEvent is null || _registeredWait is not null)
            return;

        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }
}
