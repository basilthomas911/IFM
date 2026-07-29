namespace TomasAI.IFM.Domain.Trade.Shared;

public record struct OptionLegId(
    int OrderId,
    int TradeId,
    string ContractId);
