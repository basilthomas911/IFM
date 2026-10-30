using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
namespace TomasAI.IFM.Application.Storage.MarketDataDb;

internal sealed class TickQuoteScyllaBindValue(FuturesTickQuoteDataSegment segment)
    : ScyllaTickQuoteBindValue(() =>
    {
        var owner = TickQuoteBufferEncoder.EncodePooled(segment);
        return (owner.Buffer, owner);
    });
