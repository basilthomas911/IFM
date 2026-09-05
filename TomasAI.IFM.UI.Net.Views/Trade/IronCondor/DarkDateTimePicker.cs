using System.Globalization;
using System.Runtime.InteropServices;

namespace TomasAI.IFM.UI.Net.Views.Trade.IronCondor;

/// <summary>
/// Preserves the native <see cref="DateTimePicker"/> calendar and input behavior while
/// replacing its system-colored visible face with the Trade Orders dark palette.
/// </summary>
sealed class DarkDateTimePicker : MarketData.AccessibleDateTimePicker
{
    const int WmPaint = 0x000F;
    const int WmPrintClient = 0x0318;
    const int DtmGetMonthCal = 0x1008;
    const int McmSetColor = 0x100A;
    const int ArrowAreaWidth = 24;
    static readonly Color ReadOnlyTextColor = Color.Gray;

    public DarkDateTimePicker()
    {
        BackColor = Color.Black;
        ForeColor = Color.White;
        CalendarForeColor = Color.White;
        CalendarMonthBackground = Color.Black;
        CalendarTitleBackColor = Color.Black;
        CalendarTitleForeColor = Color.White;
        CalendarTrailingForeColor = Color.Gray;
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (Width <= 0 || Height <= 0)
            return;

        if (message.Msg == WmPaint)
        {
            using var graphics = CreateGraphics();
            DrawDarkSurface(graphics);
        }
        else if (message.Msg == WmPrintClient && message.WParam != IntPtr.Zero)
        {
            using var graphics = Graphics.FromHdc(message.WParam);
            DrawDarkSurface(graphics);
        }
    }

    protected override void OnDropDown(EventArgs eventargs)
    {
        // The themed native calendar ignores most Calendar* colors. Style each new popup.
        var calendar = SendMessage(Handle, DtmGetMonthCal, IntPtr.Zero, IntPtr.Zero);
        if (calendar != IntPtr.Zero)
        {
            SetWindowTheme(calendar, string.Empty, string.Empty);
            Color[] colors = [Color.Black, CalendarForeColor, CalendarTitleBackColor,
                CalendarTitleForeColor, CalendarMonthBackground, CalendarTrailingForeColor];
            for (var index = 0; index < colors.Length; index++)
                SendMessage(calendar, McmSetColor, (IntPtr)index,
                    (IntPtr)ColorTranslator.ToWin32(colors[index]));
        }
        base.OnDropDown(eventargs);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr window, string subAppName, string subIdList);

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnValueChanged(EventArgs eventargs)
    {
        base.OnValueChanged(eventargs);
        Invalidate();
    }

    void DrawDarkSurface(Graphics graphics)
    {
        var bounds = ClientRectangle;
        using var background = new SolidBrush(Color.Black);
        graphics.FillRectangle(background, bounds);
        ControlPaint.DrawBorder(graphics, bounds, Color.Gray, ButtonBorderStyle.Solid);

        var arrowBounds = new Rectangle(
            Math.Max(1, bounds.Right - ArrowAreaWidth),
            1,
            Math.Min(ArrowAreaWidth - 1, Math.Max(0, bounds.Width - 2)),
            Math.Max(0, bounds.Height - 2));
        if (arrowBounds.Width > 0 && arrowBounds.Height > 0)
        {
            using var arrowBorder = new Pen(Color.Gray);
            graphics.DrawLine(
                arrowBorder,
                arrowBounds.Left,
                arrowBounds.Top,
                arrowBounds.Left,
                arrowBounds.Bottom - 1);
            DrawDropDownArrow(graphics, arrowBounds, Enabled ? Color.White : ReadOnlyTextColor);
        }

        var textBounds = new Rectangle(
            5,
            1,
            Math.Max(0, bounds.Width - ArrowAreaWidth - 7),
            Math.Max(0, bounds.Height - 2));
        TextRenderer.DrawText(
            graphics,
            GetDisplayText(),
            Font,
            textBounds,
            Enabled ? Color.White : ReadOnlyTextColor,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix);
    }

    string GetDisplayText()
        => Format switch
        {
            DateTimePickerFormat.Custom when !string.IsNullOrWhiteSpace(CustomFormat)
                => Value.ToString(CustomFormat, CultureInfo.CurrentCulture),
            DateTimePickerFormat.Long => Value.ToLongDateString(),
            DateTimePickerFormat.Time => Value.ToLongTimeString(),
            _ => Value.ToShortDateString(),
        };

    static void DrawDropDownArrow(Graphics graphics, Rectangle bounds, Color color)
    {
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2;
        Point[] arrow =
        [
            new(centerX - 4, centerY - 2),
            new(centerX + 4, centerY - 2),
            new(centerX, centerY + 3),
        ];
        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, arrow);
    }
}
