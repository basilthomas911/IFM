using Cassandra;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;


using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Scylla ingestion bindings, partition-safe batching and provider buffer ownership.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# 14 context extension API keeps the context receiver.")]
internal static class TradeTickQuoteDbContextExtensions
{
    extension(TradeTickQuoteDbContext context)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal Task InsertTickTradeDataAsyncCore(FuturesTickTradeDataInsertedEvent e)
        {
            var id = e.TickDataId;
            var data = e.TradeData;
        var preparedParameters1 = new InsertTickTradeData([(sbyte)e.AssetTypeId, id.ContractId, id.ValueDate, TimeOnly.FromDateTime(id.TimestampUtc), id.SequenceId, id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion, e.Dataset, e.DefinitionDate, (int)e.PublisherId, (long)e.InstrumentId, e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn, (long)data.SourceSequence, data.EventTimestampNanoseconds, data.ReceiveTimestampNanoseconds, (short)data.HeaderFlags, data.PriceRaw, data.Price, (long)data.Size, (short)data.Action, (short)data.Side, (short)data.DbnFlags]);
            return context.Use($"{nameof(TradeTickQuoteDbSql)}.{nameof(TradeTickQuoteDbSql.InsertTickTradeData)}", TradeTickQuoteDbSql.InsertTickTradeData)
            .SetParameters(preparedParameters1)
            .ExecuteCommandAsync();
        }

        /// <summary>Writes a bounded quote segment as one native CQL nested-UDT-list value.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "InsertTickQuoteData transfers the encoded value through IScyllaOwnedBindValues.BindLifetime; the Scylla executor disposes it after binding/execution, including failure.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal Task InsertTickQuoteDataAsyncCore(FuturesTickQuoteDataInsertedEvent e)
        {
            var preparedParameters = e.CreateQuoteParameters();
            return context.Use($"{nameof(TradeTickQuoteDbSql)}.{nameof(TradeTickQuoteDbSql.InsertTickQuoteData)}", TradeTickQuoteDbSql.InsertTickQuoteData)
            .SetParameters(preparedParameters)
            .ExecuteCommandAsync();
        }

        internal Task InsertFuturesTickDataAsyncCore(FuturesTickDataV2ReadModel value)
        {
            return context.InsertFuturesTickDataAsync(new[] { value });
        }

        internal async Task InsertFuturesTickDataAsyncCore(ICollection<FuturesTickDataV2ReadModel> values)
        {
            foreach (var partition in values.GroupBy(row => (row.ContractId, row.ValueDate)))
            {
                foreach (var batch in partition.Chunk(24))
                {
                    List<object> commands = [context.Use(nameof(TradeTickQuoteDbSql.InsertFuturesTickData), TradeTickQuoteDbSql.InsertFuturesTickData)
            .SetParameters(batch.Select(row => new InsertFuturesTickData(row.ContractId, row.ValueDate, row.TickId, row.TickTime, row.Price, row.Size)))
            .QueueCommand(),
                context.Use(nameof(TradeTickQuoteDbSql.InsertFuturesTickDataByTime), TradeTickQuoteDbSql.InsertFuturesTickDataByTime)
            .SetParameters(batch.Select(row => new InsertFuturesTickDataByTime(row.ContractId, row.ValueDate, row.TickTime, row.TickId, row.Price, row.Size)))
            .QueueCommand()];
                    await context.ExecuteQueuedCommandsAsync(commands, useTransaction: true)
            .ConfigureAwait(false);
                }
            }
        }

        internal Task InsertFuturesOptionTickDataAsyncCore(FuturesOptionTickDataV2ReadModel value)
        {
            return context.InsertFuturesOptionTickDataAsync(new[] { value });
        }

        internal Task InsertFuturesOptionTickDataAsyncCore(ICollection<FuturesOptionTickDataV2ReadModel> values)
        {
            return context.Use(nameof(TradeTickQuoteDbSql.InsertFuturesOptionTickData), TradeTickQuoteDbSql.InsertFuturesOptionTickData)
            .SetParameters(values.Select(row => new InsertFuturesOptionTickData(row.ContractId, row.ValueDate, row.TickId, row.TickTime, row.OptionPrice, row.BidPrice, row.AskPrice, row.BidSize, row.AskSize, row.ImpliedVolatility, row.UnderlyingPrice, row.Delta, row.Gamma, row.Vega, row.Theta, row.Rho)))
            .ExecuteCommandAsync();
        }


    }
}

internal sealed class TickQuoteScyllaBindValue(FuturesTickQuoteDataSegment segment)
    : ScyllaTickQuoteBindValue(() =>
    {
        var owner = TickQuoteBufferEncoder.EncodePooled(segment);
        return (owner.Buffer, owner);
    });



/// <summary>Retries only idempotent raw-tick upserts after a server write timeout; identities and evidence are never regenerated.</summary>
internal static class ScyllaTickWriteRetry
{
    /// <summary>Recreates each attempt's provider bindings and preserves the final failure after three attempts.</summary>
    internal static async Task ExecuteAsync(Func<Task> write, Func<TimeSpan, Task>? delay = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { await write()
            .ConfigureAwait(false); return; }
            catch (Exception error) when (attempt < 2 && IsServerWriteTimeout(error))
            {
                await (delay ?? Task.Delay)(TimeSpan.FromMilliseconds(attempt == 0 ? 100 : 400))
            .ConfigureAwait(false);
            }
        }
    }

    static bool IsServerWriteTimeout(Exception error)
    {
        while (error.InnerException is { } inner) error = inner;
        return error is WriteTimeoutException;
    }

    extension(FuturesTickQuoteDataInsertedEvent quote)
    {
        /// <summary>Prepares the raw Scylla quote insert in CQL column order and transfers UDT buffer ownership.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "The binding owns the encoded quote until execution or explicit disposal.")]
        internal InsertTickQuoteData CreateQuoteParameters()
        {
            var id = quote.TickDataId;
            var encoded = new TickQuoteScyllaBindValue(quote.QuoteData);
            return new(
                [(sbyte)quote.AssetTypeId, id.ContractId, id.ValueDate, TimeOnly.FromDateTime(id.TimestampUtc),
                    id.SequenceId, id.TimestampUtc, id.TimestampUtc.Ticks, (short)quote.SchemaVersion, quote.Dataset,
                    quote.DefinitionDate, (int)quote.PublisherId, (long)quote.InstrumentId, quote.Id, quote.EventId,
                    quote.CommandId, quote.AggregateId, quote.EventSource, quote.ReceivedOn, (short)quote.EmissionReason,
                    (short)quote.QuoteCount, encoded],
                encoded);
        }
    }

}
