using System.Runtime.InteropServices;

namespace TetherKeeper.WinUI;

/// <summary>
/// アイコンに印 (バッジ) を重ねたアイコンを作る。
/// 通信の失敗が続いているときに、トレイアイコンの右下に赤い丸を付けて知らせるために使う。
/// 画像ファイルを別に用意せず、アプリのアイコンから作るため、アイコンを変えても印の付いた絵柄が自動で合う。
/// </summary>
internal static class IconBadge
{
    // 印の色 (画面のエラー表示と同じ赤) と、縁取りの色 (背景の色に関わらず印を見分けやすくする)
    private const byte BadgeRed = 0xE5, BadgeGreen = 0x39, BadgeBlue = 0x35;
    private const byte RimRed = 0xFF, RimGreen = 0xFF, RimBlue = 0xFF;

    /// <summary>
    /// <paramref name="source"/> の右下に赤い丸を重ねたアイコンを作る。
    /// 作れなかった場合は <see cref="IntPtr.Zero"/> を返す。作ったアイコンは呼び出し元で DestroyIcon すること。
    /// </summary>
    public static IntPtr CreateAlertIcon(IntPtr source)
    {
        if (source == IntPtr.Zero || !GetIconInfo(source, out var info))
            return IntPtr.Zero;

        try
        {
            // 白黒のアイコン (カラーのビットマップが無いもの) には対応しない
            if (info.hbmColor == IntPtr.Zero || GetObject(info.hbmColor, Marshal.SizeOf<BITMAP>(), out var bitmap) == 0)
                return IntPtr.Zero;

            var width = bitmap.bmWidth;
            var height = bitmap.bmHeight;
            var pixels = ReadPixels(info.hbmColor, width, height);
            if (pixels is null)
                return IntPtr.Zero;

            // 透明度 (アルファ) を持たないアイコンは、マスクから透明な部分を求める
            if (!HasAlpha(pixels) && !ApplyMask(pixels, info.hbmMask, width, height))
                return IntPtr.Zero;

            DrawBadge(pixels, width, height);
            return CreateIcon(pixels, width, height);
        }
        finally
        {
            // GetIconInfo が作ったビットマップは、呼び出し元で削除する決まり
            if (info.hbmColor != IntPtr.Zero)
                DeleteObject(info.hbmColor);
            if (info.hbmMask != IntPtr.Zero)
                DeleteObject(info.hbmMask);
        }
    }

    /// <summary>ビットマップを、上の行から順に並んだ 32 ビット (B, G, R, A) の画素として読み出す。</summary>
    private static byte[]? ReadPixels(IntPtr hBitmap, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var header = CreateHeader(width, height);
        var hdc = GetDC(IntPtr.Zero);
        try
        {
            return GetDIBits(hdc, hBitmap, 0, (uint)height, pixels, ref header, DIB_RGB_COLORS) == height ? pixels : null;
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    private static bool HasAlpha(byte[] pixels)
    {
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
                return true;
        }

        return false;
    }

    /// <summary>マスク (白が透明、黒が不透明) から、各画素の透明度を決める。</summary>
    private static bool ApplyMask(byte[] pixels, IntPtr hMask, int width, int height)
    {
        if (hMask == IntPtr.Zero)
            return false;

        var mask = ReadPixels(hMask, width, height);
        if (mask is null)
            return false;

        for (var i = 0; i < pixels.Length; i += 4)
            pixels[i + 3] = mask[i] == 0 ? (byte)0xFF : (byte)0;
        return true;
    }

    /// <summary>右下に、白い縁取りの付いた赤い丸を描く (縁はなめらかにする)。</summary>
    private static void DrawBadge(byte[] pixels, int width, int height)
    {
        var size = Math.Min(width, height);
        var outerRadius = size * 0.3;                       // 16 px のアイコンで直径 約 10 px
        var innerRadius = outerRadius - Math.Max(1.0, size / 16.0);
        var centerX = width - outerRadius;
        var centerY = height - outerRadius;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // 画素の中心から丸の中心までの距離で、丸に含まれる割合 (0〜1) を決める
                var distance = Math.Sqrt(Math.Pow(x + 0.5 - centerX, 2) + Math.Pow(y + 0.5 - centerY, 2));
                var offset = (y * width + x) * 4;
                Blend(pixels, offset, RimRed, RimGreen, RimBlue, Coverage(outerRadius, distance));
                Blend(pixels, offset, BadgeRed, BadgeGreen, BadgeBlue, Coverage(innerRadius, distance));
            }
        }
    }

    private static double Coverage(double radius, double distance) => Math.Clamp(radius + 0.5 - distance, 0, 1);

    /// <summary>指定の色を、指定の不透明度で画素に重ねる (アイコンの画素は、透明度を掛けていない色で持つ)。</summary>
    private static void Blend(byte[] pixels, int offset, byte red, byte green, byte blue, double alpha)
    {
        if (alpha <= 0)
            return;

        var destinationAlpha = pixels[offset + 3] / 255.0;
        var outAlpha = alpha + destinationAlpha * (1 - alpha);
        byte Mix(byte source, byte destination) =>
            (byte)Math.Round((source * alpha + destination * destinationAlpha * (1 - alpha)) / outAlpha);

        pixels[offset] = Mix(blue, pixels[offset]);
        pixels[offset + 1] = Mix(green, pixels[offset + 1]);
        pixels[offset + 2] = Mix(red, pixels[offset + 2]);
        pixels[offset + 3] = (byte)Math.Round(outAlpha * 255);
    }

    private static IntPtr CreateIcon(byte[] pixels, int width, int height)
    {
        var header = CreateHeader(width, height);
        var color = CreateDIBSection(IntPtr.Zero, ref header, DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
        if (color == IntPtr.Zero)
            return IntPtr.Zero;

        // 32 ビットのカラーでは透明度が使われるため、マスクはすべて 0 (不透明) でよい
        var mask = CreateBitmap(width, height, 1, 1, new byte[((width + 15) / 16) * 2 * height]);
        try
        {
            Marshal.Copy(pixels, 0, bits, pixels.Length);
            var iconInfo = new ICONINFO { fIcon = true, hbmMask = mask, hbmColor = color };
            return mask == IntPtr.Zero ? IntPtr.Zero : CreateIconIndirect(ref iconInfo);
        }
        finally
        {
            // CreateIconIndirect はビットマップを複製するため、ここで削除してよい
            DeleteObject(color);
            if (mask != IntPtr.Zero)
                DeleteObject(mask);
        }
    }

    /// <summary>上の行から並ぶ (高さを負にする) 32 ビットのビットマップの情報。</summary>
    private static BITMAPINFOHEADER CreateHeader(int width, int height) => new()
    {
        biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
        biWidth = width,
        biHeight = -height,
        biPlanes = 1,
        biBitCount = 32,
        biCompression = BI_RGB,
    };

    #region Win32 API

    private const uint DIB_RGB_COLORS = 0;
    private const int BI_RGB = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconIndirect(ref ICONINFO info);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hObject, int size, out BITMAP bitmap);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hBitmap, uint start, uint lines, [Out] byte[] bits,
        ref BITMAPINFOHEADER header, uint usage);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER header, uint usage,
        out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitCount, byte[] bits);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    #endregion
}
