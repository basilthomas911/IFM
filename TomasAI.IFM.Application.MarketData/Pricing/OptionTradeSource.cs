using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Exact source identity. Native nanoseconds are retained without DateTime rounding.</summary>
[MessagePackObject]
public sealed record OptionTradeSource(
    [property: Key(0)] string Dataset, [property: Key(1)] ushort PublisherId,
    [property: Key(2)] uint InstrumentId, [property: Key(3)] string ContractId,
    [property: Key(4)] DateOnly ValueDate, [property: Key(5)] decimal Price,
    [property: Key(6)] uint Size, [property: Key(7)] long Sequence,
    [property: Key(8)] long EventNanoseconds, [property: Key(9)] long ReceiveNanoseconds,
    [property: Key(10)] Guid GenerationId, [property: Key(11)] string RawSymbol)
{
    [IgnoreMember]
    public string Identity => PricingSemanticHash.Compute(new
    { Dataset, PublisherId, InstrumentId, ContractId, ValueDate, Sequence, EventNanoseconds });
    // Retransmission may have a different receive time/generation but may not alter economic source data.
    [IgnoreMember]
    public string SourceDigest => PricingSemanticHash.Compute(new
    { Identity, Price, Size, RawSymbol });
}
