using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace LoudEnv.Tray;

public sealed class TrayIcons : IDisposable
{
    private readonly List<IntPtr> _handles = new();

    public TrayIcons()
    {
        Gray = Create(Color.FromArgb(128, 128, 128));
        Green = Create(Color.FromArgb(46, 160, 67));
        Yellow = Create(Color.FromArgb(210, 153, 34));
        Red = Create(Color.FromArgb(218, 54, 51));
    }

    public Icon Gray { get; }
    public Icon Green { get; }
    public Icon Yellow { get; }
    public Icon Red { get; }

    private Icon Create(Color color)
    {
        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        using var pen = new Pen(Color.FromArgb(40, 40, 40));
        graphics.FillEllipse(brush, 2, 2, 12, 12);
        graphics.DrawEllipse(pen, 2, 2, 12, 12);

        IntPtr handle = bitmap.GetHicon();
        _handles.Add(handle);
        return Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        foreach (IntPtr handle in _handles)
            DestroyIcon(handle);
        _handles.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
