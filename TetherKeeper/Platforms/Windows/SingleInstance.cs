using System.Runtime.InteropServices;

namespace TetherKeeper.WinUI;

/// <summary>
/// 多重起動の防止。トレイ常駐中に exe を再度起動した場合は、新しいプロセスは終了し、
/// 既存のプロセスに「ウィンドウを表示して」と合図を送る。
/// 既存のプロセスがウィンドウを表示したら応答 (Ack) を返し、応答が無ければ新しいプロセス側で
/// 理由をメッセージで知らせてから終了する (黙って終了すると、利用者には起動しない原因が分からないため)。
/// </summary>
internal static class SingleInstance
{
    // Local\ はログオンセッション単位 (同じ PC の別ユーザーとは干渉しない)
    private const string MutexName = @"Local\TetherKeeper.SingleInstance";
    private const string ActivateEventName = @"Local\TetherKeeper.Activate";
    private const string AckEventName = @"Local\TetherKeeper.ActivateAck";

    /// <summary>
    /// 既存プロセスからの応答を待つ時間。既存プロセスが起動処理の途中 (ウィンドウ作成前) の場合も
    /// 表示依頼は保持されて後から処理されるため、起動にかかる時間より長めにする。
    /// </summary>
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(10);

    private static Mutex? _mutex;
    private static EventWaitHandle? _activateEvent;
    private static EventWaitHandle? _ackEvent;
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
        _ackEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AckEventName);
        return true;
    }

    /// <summary>
    /// 既に起動しているインスタンスにウィンドウ表示を依頼する。
    /// 応答が無い場合 (固まっている、または別のプロセスが同じ名前を使っている) は、理由をメッセージで表示する。
    /// </summary>
    public static void SignalExistingInstance()
    {
        if (TrySignalAndWaitForAck())
            return;

        MessageBox(
            IntPtr.Zero,
            "TetherKeeper は既に起動していますが、応答がありません。\n\n" +
            "タスクマネージャーで TetherKeeper を終了してから、もう一度起動してください。\n" +
            "TetherKeeper が見当たらない場合は、PC を再起動してください。",
            "TetherKeeper",
            MB_OK | MB_ICONWARNING);
    }

    /// <summary>
    /// 他のプロセスから表示依頼が来たときの処理を登録する。
    /// コールバックはスレッドプールで呼ばれるため、UI 操作は呼び出し側で UI スレッドに移すこと。
    /// ウィンドウを表示したら <see cref="AcknowledgeActivation"/> を呼ぶこと。
    /// </summary>
    public static void ListenForActivation(Action onActivate)
    {
        if (_activateEvent is null || _registeredWait is not null)
            return;

        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>表示依頼を処理したことを、依頼元のプロセスに知らせる。</summary>
    public static void AcknowledgeActivation() => _ackEvent?.Set();

    private static bool TrySignalAndWaitForAck()
    {
        if (!EventWaitHandle.TryOpenExisting(ActivateEventName, out var activateEvent))
            return false;

        using (activateEvent)
        {
            if (!EventWaitHandle.TryOpenExisting(AckEventName, out var ackEvent))
                return false;

            using (ackEvent)
            {
                // 以前の依頼に対する応答が残っていても誤判定しないよう、先に消しておく
                ackEvent.Reset();
                activateEvent.Set();
                return ackEvent.WaitOne(AckTimeout);
            }
        }
    }

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONWARNING = 0x00000030;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
