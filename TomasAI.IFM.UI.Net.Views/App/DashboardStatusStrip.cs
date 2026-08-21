namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>A black dashboard status bar with a one-pixel gray top border.</summary>
sealed class DashboardStatusStrip : StatusStrip
{
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
        base.OnPaint(e);
        using var border = new Pen(Color.Gray);
        e.Graphics.DrawLine(border, 0, 0, ClientSize.Width - 1, 0);
    }
}
