namespace TomasAI.IFM.Shared.Trade;

public record struct OptionLegId(
    int OrderId,
    int TradeId,
    string ContractId);
