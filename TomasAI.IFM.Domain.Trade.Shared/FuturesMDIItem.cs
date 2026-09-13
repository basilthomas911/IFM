namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Defines the forward-loss limit associated with an MDI threshold.</summary>
public readonly record struct FuturesMDIItem(
    TradeType TradeType,
    int MDI,
    double ForwardLossRateLimit)
{
    public FuturesMDIId Id => new(TradeType, MDI);
}
