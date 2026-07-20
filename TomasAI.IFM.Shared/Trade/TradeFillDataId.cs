using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade;

public record TradeFillDataId(
    int OrderId, 
    int TradeId,
    string ContractId,
    DateTime FillDate)
{
    public override string ToString() => JsonConvert.SerializeObject( this, Formatting.None);
}
