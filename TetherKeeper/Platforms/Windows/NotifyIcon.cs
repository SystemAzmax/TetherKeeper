using System.Runtime.InteropServices;

namespace TetherKeeper.WinUI;

/// <summary>タスクトレイのコンテキストメニュー項目。</summary>
internal sealed record TrayMenuItem(string Text, Action? Action, bool IsDefault = false)
{
    public static TrayMenuItem Separator { get; } = new(string.Empty, null);

    public bool IsSeparator => ReferenceEquals(this, Separator);
}

/// <summary>
/// Win32 API (Shell_NotifyIcon) による通知領域 (タスクトレイ) アイコン。
/// WinUI 3 / MAUI には標準のトレイアイコン機能が無いため直接実装している。
/// UI スレッドで生成・操作すること (メッセージ処理が UI スレッドのメッセージループで行われるため)。
/// </summary>
internal sealed class NotifyIcon : IDisposable
{
    private const string WindowClassName = "TetherKeeper.NotifyIconWindow";
    private const int IconId = 1;

    // アイコンのクリック等を受け取るために独自に決めるメッセージ番号
    private const uint CallbackMessage = WM_APP + 1;

    private readonly WndProcDelegate _wndProc; // GC で回収されないようフィールドに保持する
    private readonly IntPtr _hwnd;
    private readonly IntPtr _hIcon;
    private readonly bool _ownsIcon;
    private readonly uint _taskbarCreatedMessage;
    private string _toolTip;
    private bool _disposed;

    public NotifyIcon(string iconPath, string toolTip)
    {
        _toolTip = toolTip;
        _wndProc = WindowProc;

        // トレイアイコンからの通知を受け取るための非表示ウィンドウを作る
        var hInstance = GetModuleHandle(null);
        var windowClass = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = WindowClassName,
        };
        RegisterClassEx(ref windowClass); // 2 回目以降は「登録済み」で失敗するが問題ない
        _hwnd = CreateWindowEx(0, WindowClassName, "TetherKeeper Tray", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        // エクスプローラーが再起動するとトレイアイコンが消えるため、その通知を受けて再登録する
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

        // 小アイコンのサイズ (DPI に応じた値) で .ico を読み込む。無ければ Windows 標準のアプリアイコン
        _hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON,
            GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), LR_LOADFROMFILE);
        _ownsIcon = _hIcon != IntPtr.Zero;
        if (!_ownsIcon)
            _hIcon = LoadIcon(IntPtr.Zero, IDI_APPLICATION);

        AddIcon();
    }

    /// <summary>アイコンが左クリックされた。</summary>
    public event EventHandler? Click;

    /// <summary>右クリック時に表示するメニュー項目を返す。表示のたびに呼ばれる (状態に応じて文言を変えられる)。</summary>
    public Func<IReadOnlyList<TrayMenuItem>>? MenuProvider { get; set; }

    /// <summary>マウスを重ねたときのツールチップ (最大 127 文字)。</summary>
    public string ToolTip
    {
        get => _toolTip;
        set
        {
            _toolTip = value;
            var data = CreateData(NIF_TIP);
            Shell_NotifyIcon(NIM_MODIFY, ref data);
        }
    }

    /// <summary>トレイアイコンから通知 (Windows 11 ではトースト通知) を表示する。</summary>
    public void ShowBalloon(string title, string message)
    {
        var data = CreateData(NIF_INFO);
        data.szInfoTitle = title;
        data.szInfo = message;
        data.dwInfoFlags = NIIF_INFO;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // 削除しないとプロセス終了後もマウスを重ねるまでアイコンが残る
        var data = CreateData(0);
        Shell_NotifyIcon(NIM_DELETE, ref data);
        DestroyWindow(_hwnd);
        if (_ownsIcon)
            DestroyIcon(_hIcon);
    }

    private void AddIcon()
    {
        var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP);
        Shell_NotifyIcon(NIM_ADD, ref data);
    }

    private NOTIFYICONDATA CreateData(int flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = IconId,
        uFlags = flags,
        uCallbackMessage = (int)CallbackMessage,
        hIcon = _hIcon,
        szTip = _toolTip.Length > 127 ? _toolTip[..127] : _toolTip,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == CallbackMessage)
            {
                // lParam の下位ワードに、アイコン上で発生したマウスメッセージが入る
                switch ((int)lParam.ToInt64() & 0xFFFF)
                {
                    case WM_LBUTTONUP:
                        Click?.Invoke(this, EventArgs.Empty);
                        break;
                    case WM_RBUTTONUP:
                        ShowContextMenu();
                        break;
                }

                return IntPtr.Zero;
            }

            if (msg == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
            {
                AddIcon();
                return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            // ネイティブからのコールバック内で例外を投げるとプロセスが落ちるため握りつぶす
            System.Diagnostics.Debug.WriteLine($"NotifyIcon: {ex}");
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var items = MenuProvider?.Invoke();
        if (items is null || items.Count == 0)
            return;

        var menu = CreatePopupMenu();
        try
        {
            // メニュー ID は「項目の番号 + 1」(0 は「何も選ばれなかった」を意味するため)
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].IsSeparator)
                    AppendMenu(menu, MF_SEPARATOR, 0, null);
                else
                    AppendMenu(menu, MF_STRING, (uint)(i + 1), items[i].Text);

                if (items[i].IsDefault)
                    SetMenuDefaultItem(menu, (uint)(i + 1), 0);
            }

            GetCursorPos(out var point);

            // 前面化しておかないと、メニュー外をクリックしてもメニューが閉じない (Win32 の既知の仕様)
            SetForegroundWindow(_hwnd);
            var command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, point.X, point.Y, _hwnd, IntPtr.Zero);
            PostMessage(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (command > 0)
                items[command - 1].Action?.Invoke();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    #region Win32 API

    private const uint WM_NULL = 0x0000;
    private const uint WM_APP = 0x8000;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;

    private const int NIM_ADD = 0;
    private const int NIM_MODIFY = 1;
    private const int NIM_DELETE = 2;
    private const int NIF_MESSAGE = 0x01;
    private const int NIF_ICON = 0x02;
    private const int NIF_TIP = 0x04;
    private const int NIF_INFO = 0x10;
    private const int NIIF_INFO = 0x01;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x10;
    private const int SM_CXSMICON = 49;
    private const int SM_CYSMICON = 50;
    private static readonly IntPtr IDI_APPLICATION = new(32512);

    private const uint MF_STRING = 0x0000;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_NONOTIFY = 0x0080;
    private const uint TPM_RETURNCMD = 0x0100;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, uint id, string? text);

    [DllImport("user32.dll")]
    private static extern bool SetMenuDefaultItem(IntPtr menu, uint item, uint byPosition);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hWnd, IntPtr tpmParams);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    #endregion
}
