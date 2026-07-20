using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade;

public record struct TradeFillId(
    int OrderId, 
    int TradeId,
    DateTime FillDate)
{
    public override string ToString() => JsonConvert.SerializeObject( this, Formatting.None);
}
