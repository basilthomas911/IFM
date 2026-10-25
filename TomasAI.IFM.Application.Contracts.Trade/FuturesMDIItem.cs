namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Defines the forward-loss limit associated with an MDI threshold.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve this existing persisted CLR type identity and acronym spelling.")]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct FuturesMDIItem(
    TradeType TradeType,
    int MDI,
    double ForwardLossRateLimit)
{
    public FuturesMDIId Id => new(TradeType, MDI);
}
