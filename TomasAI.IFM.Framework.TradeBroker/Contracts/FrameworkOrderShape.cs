namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>The exact first-release strategy shape supported by a provider.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
public enum FrameworkOrderShape : byte
{
    Unknown = 0,
    FuturesOutright = 1,
    VerticalSpread = 2,
    IronCondor = 3
}
