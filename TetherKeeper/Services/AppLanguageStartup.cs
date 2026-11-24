using System.Globalization;
using System.Text.Json;
using KeepAlive.Core.Models;

namespace TetherKeeper.Services;

/// <summary>
/// 設定で選んだ表示言語 (<see cref="AppSettings.Language"/>) を、起動のいちばん初めに反映する。
/// 画面の文言はページを作るときに読み込むため、設定の読み込み (非同期) を待たずに、設定ファイルから直接読む。
/// </summary>
public static class AppLanguageStartup
{
    /// <summary>保存されている言語を反映する。言語が空 (Windows の表示言語に合わせる) や読めない場合は何もしない。</summary>
    public static void ApplySavedLanguage()
    {
        try
        {
            Apply(ReadSavedLanguage(AppPaths.SettingsFile));
        }
        catch (Exception ex)
        {
            // 言語を反映できなくても、Windows の表示言語で起動を続ける
            System.Diagnostics.Debug.WriteLine($"AppLanguageStartup: 表示言語を反映できませんでした: {ex}");
        }
    }

    /// <summary>
    /// 指定した言語を、このあと作るすべてのスレッドの表示言語にする。用意していない言語なら何もしない。
    /// 数値や日時の書式 (<see cref="CultureInfo.CurrentCulture"/>) は、Windows の地域の設定のままにする。
    /// </summary>
    public static void Apply(string? code)
    {
        if (AppLanguages.Resolve(code) is not { } culture)
            return;

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>設定ファイルの Language の値。ファイルが無い、壊れている場合は null。</summary>
    internal static string? ReadSavedLanguage(string settingsFile)
    {
        if (!File.Exists(settingsFile))
            return null;

        try
        {
            using var stream = new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(nameof(AppSettings.Language), out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
