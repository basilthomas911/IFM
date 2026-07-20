namespace TomasAI.IFM.Shared.Trade;

public record struct OptionLegDataId(
    int OrderId,
    int TradeId,
    DateOnly ValueDate,
    TradeType TradeType,
    int DaysToExpiry,
    TradeStatus TradeStatus,
    string OptionLegId);
