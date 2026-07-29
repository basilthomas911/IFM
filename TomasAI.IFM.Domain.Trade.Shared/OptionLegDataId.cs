using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.Domain.Trade.Shared;

public record struct OptionLegDataId(
    int OrderId,
    int TradeId,
    DateOnly ValueDate,
    TradeType TradeType,
    int DaysToExpiry,
    TradeStatus TradeStatus,
    string OptionLegId);
