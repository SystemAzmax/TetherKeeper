using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepAlive.Core.Interfaces;
using KeepAlive.Core.Models;
using KeepAlive.Core.Services;
using TetherKeeper.Services;

namespace TetherKeeper.ViewModels;

/// <summary>集計する期間の選択肢。</summary>
public sealed record HistoryPeriod(string DisplayName, TimeSpan Length, string FileNamePart);

/// <summary>時間帯の集計の 1 行 (例: 09 時 / 120 回中 5 回失敗 (4.2%))。</summary>
/// <param name="BarWidth">失敗率の棒の長さ (失敗率が最も高い時間帯を最大にする)。</param>
public sealed record HourRow(string HourText, string CountText, double BarWidth, bool HasFailures);

public partial class HistoryViewModel : ObservableObject
{
    private const double MaxBarWidth = 160;

    private readonly ResultHistory _history;
    private readonly ILogService _log;
    private IReadOnlyList<KeepAliveResult> _results = [];

    public HistoryViewModel(ResultHistory history, ILogService log)
    {
        _history = history;
        _log = log;
        SelectedPeriod = Periods[1];
    }

    public IReadOnlyList<HistoryPeriod> Periods { get; } =
    [
        new(AppStrings.History_Last24Hours, TimeSpan.FromHours(24), AppStrings.History_File24Hours),
        new(AppStrings.History_Last7Days, TimeSpan.FromDays(7), AppStrings.History_File7Days),
        new(AppStrings.History_Last30Days, TimeSpan.FromDays(30), AppStrings.History_File30Days),
    ];

    [ObservableProperty]
    public partial HistoryPeriod SelectedPeriod { get; set; }

    /// <summary>期間の合計 (例: 合計 1,234 回 / 成功 1,200 回 / 失敗 34 回 (2.8%))。</summary>
    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<HourRow> HourRows { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string StatusText { get; set; } = string.Empty;

    public bool HasStatus => StatusText.Length > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial bool HasResults { get; set; }

    partial void OnSelectedPeriodChanged(HistoryPeriod value) => _ = ReloadAsync();

    /// <summary>選んだ期間の履歴を読み直して集計する。</summary>
    [RelayCommand]
    public async Task ReloadAsync()
    {
        try
        {
            var period = SelectedPeriod;
            _results = await _history.LoadAsync(period.Length);
            ShowSummary(ResultHistory.Summarize(_results, TimeZoneInfo.Local));
            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            _log.Error(Text.Format(AppStrings.History_LoadFailedLog, ex.Message));
            StatusText = Text.Format(AppStrings.History_LoadFailed, ex.Message);
        }
    }

    /// <summary>選んだ期間の結果を、ダウンロード フォルダーに CSV で書き出し、エクスプローラーで表示する。</summary>
    [RelayCommand(CanExecute = nameof(HasResults))]
    private async Task ExportAsync()
    {
        var path = Path.Combine(
            AppPaths.ExportDirectory,
            Text.Format(AppStrings.History_FileName, SelectedPeriod.FileNamePart, DateTime.Now));
        try
        {
            Directory.CreateDirectory(AppPaths.ExportDirectory);
            await using (var stream = File.Create(path))
            {
                await ResultHistory.WriteCsvAsync(_results, stream, TimeZoneInfo.Local);
            }

            _log.Info(Text.Format(AppStrings.History_ExportedLog, path));
            StatusText = Text.Format(AppStrings.History_Exported, path);
            ShowInExplorer(path);
        }
        catch (Exception ex)
        {
            _log.Error(Text.Format(AppStrings.History_ExportFailedLog, ex.Message));
            StatusText = Text.Format(AppStrings.History_ExportFailed, ex.Message);
        }
    }

    private void ShowSummary(HistorySummary summary)
    {
        HasResults = summary.Total > 0;
        if (summary.Total == 0)
        {
            SummaryText = AppStrings.History_NoRecords;
            HourRows = [];
            return;
        }

        var fallback = summary.FallbackSuccesses > 0
            ? Text.Format(AppStrings.History_FallbackPart, summary.FallbackSuccesses)
            : string.Empty;
        SummaryText = Text.Format(
            AppStrings.History_Summary, summary.Total, summary.Successes, fallback, summary.Failures, summary.FailureRate);

        // 棒は失敗率で比べる (時間帯によって通信した回数が違うため、回数より割合の方が比べやすい)
        var maxRate = summary.ByHour.Max(h => h.FailureRate);
        HourRows = summary.ByHour
            .Select(h => new HourRow(
                Text.Format(AppStrings.History_Hour, h.Hour),
                h.Total == 0
                    ? AppStrings.History_NoRecordsHour
                    : Text.Format(AppStrings.History_HourCount, h.Total, h.Failures, h.FailureRate),
                maxRate > 0 ? MaxBarWidth * h.FailureRate / maxRate : 0,
                h.Failures > 0))
            .ToList();
    }

    private void ShowInExplorer(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            else
                Process.Start(new ProcessStartInfo(Path.GetDirectoryName(path)!) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // 書き出しはできているため、フォルダを開けなくてもエラーにはしない
            _log.Warning(Text.Format(AppStrings.History_OpenFolderFailed, ex.Message));
        }
    }
}
