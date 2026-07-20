using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataFeed;

public record FuturesOptionTickDataId(string ContractId, DateOnly ValueDate, long TickId)
{
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
    public static FuturesOptionTickDataId Create(string contractId, DateOnly valueDate, long tickId) => new FuturesOptionTickDataId(contractId, valueDate, tickId);

}
