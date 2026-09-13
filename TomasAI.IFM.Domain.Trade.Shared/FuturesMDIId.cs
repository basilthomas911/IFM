namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies one MDI threshold for a trade strategy type.</summary>
public readonly record struct FuturesMDIId(TradeType TradeType, int MDI);
