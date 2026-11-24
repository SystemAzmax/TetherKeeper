using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;

namespace TetherKeeper.Services;

/// <summary>
/// アプリの「本当の終了」を担う。
/// Windows では × ボタンはタスクトレイへの格納になるため、終了はこのサービス経由でのみ行う。
/// 設定 (<see cref="AppSettings.ExitAppOnAutoStop"/>) がオンなら、自動停止したときにも終了する。
/// </summary>
public sealed class AppExitService
{
    private readonly IKeepAliveWorker _worker;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;

    public AppExitService(IKeepAliveWorker worker, ISettingsService settings, ILogService log)
    {
        _worker = worker;
        _settings = settings;
        _log = log;
        _worker.AutoStopped += OnAutoStopped;
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
            _log.Info($"{AppDisplayName.JAPANESE}を終了します。");

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

    /// <summary>
    /// 日時の予約か毎週の自動停止で停止したら、設定に従ってアプリも終了する。
    /// 通信量の上限で停止したときは、理由を画面で確かめられるよう終了しない。
    /// </summary>
    private void OnAutoStopped(object? sender, AutoStopReason reason)
    {
        if (reason == AutoStopReason.DataLimit || !_settings.Current.ExitAppOnAutoStop)
            return;

        _log.Info("自動停止したため、アプリを終了します。");
        // Worker のスレッドから呼ばれるため、終了は UI スレッドで行う
        MainThread.BeginInvokeOnMainThread(() => _ = ExitAsync());
    }
}
