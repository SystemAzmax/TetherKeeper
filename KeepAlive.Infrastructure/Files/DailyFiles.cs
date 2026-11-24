using System.Globalization;

namespace KeepAlive.Infrastructure.Files;

/// <summary>
/// 日付ごとのファイル (例: TetherKeeper_20261008.log) の名前の決まりと、古いファイルの削除。
/// ログ (<see cref="Logging.FileLogSink"/>) と通信の結果の履歴 (<see cref="History.CsvResultHistoryStore"/>) で使う。
/// </summary>
internal static class DailyFiles
{
    private const string DateFormat = "yyyyMMdd";

    /// <summary>指定日のファイルのパス。</summary>
    public static string GetPath(string directory, string prefix, string extension, DateOnly date) =>
        Path.Combine(directory, $"{prefix}_{date.ToString(DateFormat, CultureInfo.InvariantCulture)}{extension}");

    /// <summary>フォルダにある、名前の決まりに合うファイルと、その日付。</summary>
    public static IEnumerable<(string Path, DateOnly Date)> Enumerate(string directory, string prefix, string extension)
    {
        if (!Directory.Exists(directory))
            yield break;

        foreach (var path in Directory.EnumerateFiles(directory, $"{prefix}_*{extension}"))
        {
            var datePart = Path.GetFileNameWithoutExtension(path)[(prefix.Length + 1)..];
            if (DateOnly.TryParseExact(datePart, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                yield return (path, date);
        }
    }

    /// <summary>今日を含めて <paramref name="retentionDays"/> 日より古いファイルを削除する。消せないファイルは次回に持ち越す。</summary>
    public static void DeleteOlderThan(string directory, string prefix, string extension, DateOnly today, int retentionDays)
    {
        var oldestKept = today.AddDays(-(Math.Max(1, retentionDays) - 1));
        foreach (var (path, date) in Enumerate(directory, prefix, extension).ToList())
        {
            if (date >= oldestKept)
                continue;

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // 閲覧中などで消せない場合は次回に持ち越す
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
