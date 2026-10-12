namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>One exact contract and signed quantity in a broker-neutral request.</summary>
public sealed record FrameworkOrderLeg(
    Guid LegId,
    string ContractId,
    int SignedQuantity,
    decimal? Strike,
    DateOnly? Expiry,
    byte? PutCall,
    decimal CashMultiplier);
