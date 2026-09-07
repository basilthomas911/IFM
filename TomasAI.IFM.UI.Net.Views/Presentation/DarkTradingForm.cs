namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>Default base for every application-owned window and dialog.</summary>
public class DarkTradingForm : Form
{
    public DarkTradingForm()
    {
        Font = DarkTradingTheme.CreateFont();
        BackColor = DarkTradingTheme.Background;
        ForeColor = DarkTradingTheme.Foreground;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        if (e.Control is not null) DarkTradingTheme.Apply(e.Control);
        base.OnControlAdded(e);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        DarkTradingTypography.NormalizeControlFont(this);
        base.OnFontChanged(e);
    }

    protected override void OnCreateControl()
    {
        DarkTradingTheme.Apply(this);
        EnsureFrame();
        base.OnCreateControl();
    }

    protected override void OnLoad(EventArgs e)
    {
        DarkTradingTheme.Apply(this);
        EnsureFrame();
        base.OnLoad(e);
    }

    void EnsureFrame()
    {
        // Existing framed forms already reserve their own three-pixel inset.
        if (Controls.Cast<Control>().Any(DarkTradingTheme.IsFrame)) return;
        var width = DarkTradingTheme.FrameWidth;
        Padding = new Padding(Math.Max(Padding.Left, width), Math.Max(Padding.Top, width),
            Math.Max(Padding.Right, width), Math.Max(Padding.Bottom, width));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Controls.Cast<Control>().Any(DarkTradingTheme.IsFrame)) return;
        var width = DarkTradingTheme.FrameWidth;
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
            DarkTradingTheme.Border, width, ButtonBorderStyle.Solid,
            DarkTradingTheme.Border, width, ButtonBorderStyle.Solid,
            DarkTradingTheme.Border, width, ButtonBorderStyle.Solid,
            DarkTradingTheme.Border, width, ButtonBorderStyle.Solid);
    }
}
