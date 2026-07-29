using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.Trade.Shared;

public record struct TradeFillId(
    int OrderId, 
    int TradeId,
    DateTime FillDate)
{
    public override string ToString() => JsonConvert.SerializeObject( this, Formatting.None);
}
