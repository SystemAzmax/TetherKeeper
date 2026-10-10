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

        // 途中で何が失敗しても必ず終了する (IsExiting が true のままだと「終了」が二度と効かなくなるため)
        try
        {
            // 停止が終わらない場合も終了は妨げない
            await Task.WhenAny(_worker.StopAsync(), Task.Delay(TimeSpan.FromSeconds(5)));
            _log.Info("TetherKeeper を終了します。");

            // ログファイルへの書き残しが無いようにしてから終了する (ファイル書き込みが詰まっても終了は妨げない)
            await Task.WhenAny(_log.FlushAsync(), Task.Delay(TimeSpan.FromSeconds(3)));
        }
        catch (Exception ex)
        {
            _log.Error($"終了処理中にエラーが発生しました: {ex.Message}");
        }

        try
        {
            Exiting?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            Application.Current?.Quit();
        }
    }
}
