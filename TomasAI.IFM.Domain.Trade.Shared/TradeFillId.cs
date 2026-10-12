using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.Trade.Shared;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public record struct TradeFillId(
    int OrderId,
    int TradeId,
    DateTime FillDate)
{
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
