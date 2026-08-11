using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.Views.Presentation;

/// <summary>
/// Maps framework-neutral color roles to the current WinForms palette.
/// </summary>
public static class WinFormsPresentationColorExtensions
{
    public static Color ToColor(this PresentationColorRole role)
        => role switch
        {
            PresentationColorRole.DarkText => Color.Black,
            PresentationColorRole.LightText => Color.White,
            PresentationColorRole.Positive => Color.LimeGreen,
            PresentationColorRole.PositiveMuted => Color.YellowGreen,
            PresentationColorRole.Caution => Color.Yellow,
            PresentationColorRole.Warning => Color.DarkOrange,
            PresentationColorRole.NegativeMuted => Color.OrangeRed,
            PresentationColorRole.Negative => Color.Red,
            PresentationColorRole.DarkSurface => Color.Black,
            _ => SystemColors.Control
        };
}
