using System.Globalization;
using System.Text;
using KeepAlive.Core.Models;

namespace KeepAlive.Core.Services;

/// <summary>
/// 通信の結果の履歴を CSV の 1 行にする・CSV の 1 行から読む。
/// 履歴のファイルと、書き出す CSV の両方で同じ形式を使う。
/// Excel で開きやすいよう、日時は「2026/11/24 10:23:45」(PC の時刻) の形にし、文字列は必要なときだけ「"」で囲む。
/// </summary>
public static class ResultCsv
{
    private const string TimestampFormat = "yyyy/MM/dd HH:mm:ss";
    private const string SuccessText = "成功";
    private const string FailureText = "失敗";
    private const string FallbackText = "予備で成功";

    /// <summary>見出しの行。</summary>
    public const string Header = "日時,結果,通信方式,送信先,応答時間(ms),予備,内容";

    /// <summary>結果を CSV の 1 行にする。日時は <paramref name="zone"/> の時刻で書く。</summary>
    public static string Format(KeepAliveResult result, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(result.Timestamp, zone);
        return string.Join(',',
            local.ToString(TimestampFormat, CultureInfo.InvariantCulture),
            result.Success ? SuccessText : FailureText,
            Escape(result.StrategyName),
            Escape(result.Target),
            ((long)Math.Round(result.Elapsed.TotalMilliseconds)).ToString(CultureInfo.InvariantCulture),
            result.UsedFallback ? FallbackText : string.Empty,
            Escape(result.Message ?? string.Empty));
    }

    /// <summary>CSV の 1 行を結果に戻す。見出しの行や壊れた行は null。日時は <paramref name="zone"/> の時刻とみなす。</summary>
    public static KeepAliveResult? Parse(string line, TimeZoneInfo zone)
    {
        var fields = SplitFields(line);
        if (fields is not { Count: 7 }
            || !DateTime.TryParseExact(fields[0], TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            || fields[1] is not (SuccessText or FailureText)
            || !long.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var elapsedMs))
            return null;

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new KeepAliveResult
        {
            Timestamp = new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified)),
            Success = fields[1] == SuccessText,
            StrategyName = fields[2],
            Target = fields[3],
            Elapsed = TimeSpan.FromMilliseconds(elapsedMs),
            UsedFallback = fields[5] == FallbackText,
            Message = fields[6].Length > 0 ? fields[6] : null,
        };
    }

    /// <summary>カンマ、「"」、改行を含む値は「"」で囲み、中の「"」は 2 つ重ねる (RFC 4180)。</summary>
    private static string Escape(string value)
    {
        // 改行は 1 行に収めるため空白にする (履歴は 1 結果 1 行で読むため)
        value = value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        return value.IndexOfAny([',', '"']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    /// <summary>1 行をフィールドに分ける。「"」で囲まれたフィールドの中のカンマと「""」を扱う。不正な行は null。</summary>
    private static List<string>? SplitFields(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var fieldStart = true;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c != '"')
                {
                    current.Append(c);
                }
                else if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    // 囲みの中の「""」は「"」1 つ
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            switch (c)
            {
                case '"' when fieldStart:
                    quoted = true;
                    fieldStart = false;
                    break;
                case ',':
                    fields.Add(current.ToString());
                    current.Clear();
                    fieldStart = true;
                    break;
                default:
                    current.Append(c);
                    fieldStart = false;
                    break;
            }
        }

        if (quoted)
            return null;
        fields.Add(current.ToString());
        return fields;
    }
}
