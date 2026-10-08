using System.Runtime.Versioning;
using KeepAlive.Core.Interfaces;
using Microsoft.Win32;

namespace KeepAlive.Infrastructure.AutoStart;

/// <summary>
/// HKCU\...\Run への登録によるログイン時自動起動 (管理者権限不要)。
/// MSIX パッケージ版では Run キーは使えないため、パッケージ化する場合は StartupTask 拡張に置き換えること。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;
    private readonly string _executablePath;
    private readonly string? _arguments;

    /// <param name="arguments">起動時に渡す引数 (例: トレイに格納した状態で起動する指定)。</param>
    public WindowsAutoStartService(string valueName, string executablePath, string? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        _valueName = valueName;
        _executablePath = executablePath;
        _arguments = arguments;
    }

    public bool IsSupported => true;

    private string CommandLine => string.IsNullOrWhiteSpace(_arguments)
        ? $"\"{_executablePath}\""
        : $"\"{_executablePath}\" {_arguments}";

    public Task<bool> IsEnabledAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var enabled = key?.GetValue(_valueName) is string value
            && string.Equals(value, CommandLine, StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(enabled);
    }

    public Task SetEnabledAsync(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
            key.SetValue(_valueName, CommandLine, RegistryValueKind.String);
        else
            key.DeleteValue(_valueName, throwOnMissingValue: false);
        return Task.CompletedTask;
    }
}
