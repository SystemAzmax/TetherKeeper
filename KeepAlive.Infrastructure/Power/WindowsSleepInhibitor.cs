using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using KeepAlive.Core.Interfaces;

namespace KeepAlive.Infrastructure.Power;

/// <summary>
/// 電源要求 (PowerCreateRequest / PowerSetRequest の PowerRequestSystemRequired) で、自動スリープを抑止する。
/// </summary>
/// <remarks>
/// <para>
/// SetThreadExecutionState と違い、電源要求は呼び出したスレッドに結び付かないため、
/// スレッドプールのどのスレッドから有効・無効を切り替えてもよい。
/// また、理由の文字列が <c>powercfg /requests</c> に表示されるため、スリープしない原因を利用者が確かめられる。
/// </para>
/// <para>
/// Windows の仕様により、利用者の操作 (電源ボタン、ふたを閉じる、スタートメニューの「スリープ」) によるスリープは防げない。
/// また、モダン スタンバイの PC をバッテリーで使っている場合は、スリープまでの時間を過ぎてから 5 分で要求が打ち切られる。
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsSleepInhibitor : ISleepInhibitor, IDisposable
{
    private readonly object _gate = new();
    private readonly string _reason;
    private IntPtr _reasonText;
    private IntPtr _request;
    private bool _enabled;
    private bool _disposed;

    /// <param name="reason"><c>powercfg /requests</c> に表示される、スリープを抑止している理由。</param>
    public WindowsSleepInhibitor(string reason)
    {
        _reason = reason;
    }

    public bool IsSupported => true;

    public void SetEnabled(bool enabled)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_enabled == enabled)
                return;

            if (enabled)
            {
                EnsureRequest();
                if (!PowerSetRequest(_request, PowerRequestSystemRequired))
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
            else if (_request != IntPtr.Zero && !PowerClearRequest(_request, PowerRequestSystemRequired))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            _enabled = enabled;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;

            // ハンドルを閉じると、有効にしていた要求も解除される
            if (_request != IntPtr.Zero)
                CloseHandle(_request);
            if (_reasonText != IntPtr.Zero)
                Marshal.FreeHGlobal(_reasonText);
            _request = IntPtr.Zero;
            _reasonText = IntPtr.Zero;
        }
    }

    private void EnsureRequest()
    {
        if (_request != IntPtr.Zero)
            return;

        // 理由の文字列は、要求を閉じるまで確保しておく
        if (_reasonText == IntPtr.Zero)
            _reasonText = Marshal.StringToHGlobalUni(_reason);

        var context = new REASON_CONTEXT
        {
            Version = POWER_REQUEST_CONTEXT_VERSION,
            Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING,
            SimpleReasonString = _reasonText,
        };
        var request = PowerCreateRequest(ref context);
        if (request == IntPtr.Zero || request == INVALID_HANDLE_VALUE)
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        _request = request;
    }

    #region Win32 API

    private const uint POWER_REQUEST_CONTEXT_VERSION = 0;
    private const uint POWER_REQUEST_CONTEXT_SIMPLE_STRING = 0x1;
    private const int PowerRequestSystemRequired = 1;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    /// <summary>
    /// REASON_CONTEXT。共用体 (Reason) は SimpleReasonString だけを使うが、
    /// 大きい方 (Detailed: HMODULE, ULONG, ULONG, LPWSTR*) と同じ大きさにしておく。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct REASON_CONTEXT
    {
        public uint Version;
        public uint Flags;
        public IntPtr SimpleReasonString;
        public uint Reserved1;
        public uint Reserved2;
        public IntPtr Reserved3;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr PowerCreateRequest(ref REASON_CONTEXT context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(IntPtr powerRequest, int requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(IntPtr powerRequest, int requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    #endregion
}
