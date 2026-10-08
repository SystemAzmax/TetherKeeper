using KeepAlive.Core.Interfaces;

namespace TetherKeeper.Services;

/// <summary>
/// アプリの「本当の終了」を担う。
/// Windows では × ボタンはタスクトレイへの格納になるため、終了はこのサービス経由でのみ行う。
/// </summary>
public sealed class AppExitService
{
    private readonly IKeepAliveWorker _worker;
    private readonly ILogService _log;

    public AppExitService(IKeepAliveWorker worker, ILogService log)
    {
        _worker = worker;
        _log = log;
    }

    /// <summary>終了処理中か。ウィンドウを閉じる操作をトレイ格納ではなく終了として扱うかの判定に使う。</summary>
    public bool IsExiting { get; private set; }

    /// <summary>終了直前に発生する (タスクトレイアイコンの削除など後片付け用)。</summary>
    public event EventHandler? Exiting;

    public async Task ExitAsync()
    {
        if (IsExiting)
            return;
        IsExiting = true;

        await _worker.StopAsync();
        _log.Info("TetherKeeper を終了します。");

        // ログファイルへの書き残しが無いようにしてから終了する (ファイル書き込みが詰まっても終了は妨げない)
        await Task.WhenAny(_log.FlushAsync(), Task.Delay(TimeSpan.FromSeconds(3)));

        Exiting?.Invoke(this, EventArgs.Empty);
        Application.Current?.Quit();
    }
}
