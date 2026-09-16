using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage.ScyllaDb;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

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

    /// <summary>Writes a bounded quote segment as one native CQL nested-UDT-list value.</summary>
    public Task InsertTickQuoteDataAsync(FuturesTickQuoteDataInsertedEvent e)
    {
        var id = e.TickDataId;
        var encoded = new TickQuoteEncodedStorageCollection(e.QuoteData);
        object?[] values = [
            (sbyte)e.AssetTypeId, id.ContractId, id.ValueDate,
            TimeOnly.FromDateTime(id.TimestampUtc), id.SequenceId,
            id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion,
            e.Dataset, e.DefinitionDate, (int)e.PublisherId, (long)e.InstrumentId,
            e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn,
            (short)e.EmissionReason, (short)e.QuoteCount,
            encoded
        ];
        return Use($"{nameof(MarketDataDbCql)}.{nameof(MarketDataDbCql.InsertTickQuoteData)}", MarketDataDbCql.InsertTickQuoteData)
            .SetParameters(new InsertTickQuoteData(values, encoded))
            .ExecuteCommandAsync();
    }
}
