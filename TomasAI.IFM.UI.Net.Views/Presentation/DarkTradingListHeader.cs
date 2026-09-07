using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>Paints the native header's unused area without changing user column widths.</summary>
internal sealed class DarkTradingListHeader : NativeWindow
{
    static readonly ConditionalWeakTable<ListView, DarkTradingListHeader> Headers = new();
    readonly ListView _list;

    DarkTradingListHeader(ListView list)
    {
        _list = list;
        list.HandleCreated += (_, _) => AttachHandle();
        list.Disposed += (_, _) => ReleaseHandle();
        AttachHandle();
    }

    internal static void Apply(ListView list) => Headers.GetValue(list, value => new(value)).AttachHandle();

    void AttachHandle()
    {
        if (!_list.IsHandleCreated || _list.IsDisposed) return;
        // LVM_GETHEADER = LVM_FIRST + 31.
        var header = SendMessage(_list.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero);
        if (header == IntPtr.Zero || header == Handle) return;
        ReleaseHandle();
        AssignHandle(header);
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (Handle == IntPtr.Zero || _list.IsDisposed) return;
        if (message.Msg == 0x000F) // WM_PAINT
        {
            using var graphics = Graphics.FromHwnd(Handle);
            PaintUnusedHeader(graphics);
        }
        else if (message.Msg is 0x0317 or 0x0318 && message.WParam != IntPtr.Zero) // WM_PRINT / WM_PRINTCLIENT
        {
            using var graphics = Graphics.FromHdc(message.WParam);
            PaintUnusedHeader(graphics);
        }
    }

    void PaintUnusedHeader(Graphics graphics)
    {
        if (!GetClientRect(Handle, out var rect)) return;
        var left = Math.Max(0, _list.Columns.Cast<ColumnHeader>().Sum(column => column.Width)
            - GetScrollPos(_list.Handle, 0));
        if (rect.Right <= left || rect.Bottom <= 0) return;
        var bounds = new Rectangle(left, 0, rect.Right - left, rect.Bottom);
        using var background = new SolidBrush(DarkTradingTheme.CommandSurface);
        graphics.FillRectangle(background, bounds);
        ControlPaint.DrawBorder(graphics, bounds, DarkTradingTheme.Border, ButtonBorderStyle.Solid);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    static extern int GetScrollPos(IntPtr window, int bar);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetClientRect(IntPtr window, out NativeRect rect);
}
