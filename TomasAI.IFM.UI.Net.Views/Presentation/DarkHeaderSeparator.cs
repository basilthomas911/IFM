namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>A theme-safe horizontal separator used between trading-view headers and tab workspaces.</summary>
internal sealed class DarkHeaderSeparator : Control
{
    private static readonly Color LineColor = Color.FromArgb(118, 148, 178);

    public DarkHeaderSeparator()
    {
        Name = "headerSeparator";
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var brush = new SolidBrush(LineColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}
