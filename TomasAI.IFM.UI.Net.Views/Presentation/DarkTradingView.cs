namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>Default base for embedded views, including asynchronously loaded editors.</summary>
public class DarkTradingView : UserControl
{
    public DarkTradingView()
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
        base.OnCreateControl();
    }
}
