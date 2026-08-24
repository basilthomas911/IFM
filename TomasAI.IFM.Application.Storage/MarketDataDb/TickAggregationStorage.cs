using Cassandra;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage.ScyllaDb;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

internal sealed class TickQuoteStorageCollection(FuturesTickQuoteDataSegment segment) : IScyllaUdtValue
{
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<ISession, object> MappedSessions = new();

    public object Resolve(ISession session)
    {
        lock (Gate)
        {
            if (!MappedSessions.TryGetValue(session, out _))
            {
                session.UserDefinedTypes.Define(
                    UdtMap.For<TickQuoteStorageItem>("tick_quote_item")
                        .Map(x => x.SourceSequence, "source_sequence")
                        .Map(x => x.SourceEventTimestampNanoseconds, "source_event_timestamp_ns")
                        .Map(x => x.SourceReceiveTimestampNanoseconds, "source_receive_timestamp_ns")
                        .Map(x => x.HeaderFlags, "header_flags")
                        .Map(x => x.BidPriceRaw, "bid_price_raw")
                        .Map(x => x.BidPrice, "bid_price")
                        .Map(x => x.BidSize, "bid_size")
                        .Map(x => x.BidCount, "bid_count")
                        .Map(x => x.AskPriceRaw, "ask_price_raw")
                        .Map(x => x.AskPrice, "ask_price")
                        .Map(x => x.AskSize, "ask_size")
                        .Map(x => x.AskCount, "ask_count"));
                MappedSessions.Add(session, new object());
            }
        }
        var values = new TickQuoteStorageItem[segment.Count];
        for (var index = 0; index < values.Length; index++)
        {
            var quote = segment.Buffer[index];
            values[index] = new TickQuoteStorageItem
            {
                SourceSequence = quote.SourceSequence,
                SourceEventTimestampNanoseconds = quote.EventTimestampNanoseconds,
                SourceReceiveTimestampNanoseconds = quote.ReceiveTimestampNanoseconds,
                HeaderFlags = quote.HeaderFlags,
                BidPriceRaw = quote.BidPriceRaw, BidPrice = quote.BidPrice,
                BidSize = quote.BidSize, BidCount = quote.BidCount,
                AskPriceRaw = quote.AskPriceRaw, AskPrice = quote.AskPrice,
                AskSize = quote.AskSize, AskCount = quote.AskCount
            };
        }
        return values;
    }
}

internal sealed class TickQuoteStorageItem
{
    public long SourceSequence { get; set; }
    public long SourceEventTimestampNanoseconds { get; set; }
    public long SourceReceiveTimestampNanoseconds { get; set; }
    public short HeaderFlags { get; set; }
    public long BidPriceRaw { get; set; }
    public decimal? BidPrice { get; set; }
    public long BidSize { get; set; }
    public long BidCount { get; set; }
    public long AskPriceRaw { get; set; }
    public decimal? AskPrice { get; set; }
    public long AskSize { get; set; }
    public long AskCount { get; set; }
}

public partial class MarketDataDbContext
{
    public Task InsertTickTradeDataAsync(FuturesTickTradeDataInsertedEvent e)
    {
        var id = e.TickDataId;
        var data = e.TradeData;
        return Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertTickTradeData)}", MarketDataDbCql.InsertTickTradeData)
            .SetParameters(new InsertTickTradeData([
                (sbyte)e.AssetTypeId, id.ContractId, id.ValueDate,
                TimeOnly.FromDateTime(id.TimestampUtc), id.SequenceId,
                id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion,
                e.Dataset, e.DefinitionDate, (int)e.PublisherId, (long)e.InstrumentId,
                e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn,
                (long)data.SourceSequence, data.EventTimestampNanoseconds,
                data.ReceiveTimestampNanoseconds, (short)data.HeaderFlags,
                data.PriceRaw, data.Price, (long)data.Size, (short)data.Action,
                (short)data.Side, (short)data.DbnFlags
            ])).ExecuteCommandAsync();
    }

    public Task InsertTickQuoteDataAsync(FuturesTickQuoteDataInsertedEvent e)
    {
        var id = e.TickDataId;
        return Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertTickQuoteData)}", MarketDataDbCql.InsertTickQuoteData)
            .SetParameters(new InsertTickQuoteData([
                (sbyte)e.AssetTypeId, id.ContractId, id.ValueDate,
                TimeOnly.FromDateTime(id.TimestampUtc), id.SequenceId,
                id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion,
                e.Dataset, e.DefinitionDate, (int)e.PublisherId, (long)e.InstrumentId,
                e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn,
                (short)e.EmissionReason, (short)e.QuoteCount,
                new TickQuoteStorageCollection(e.QuoteData)
            ])).ExecuteCommandAsync();
    }
}
