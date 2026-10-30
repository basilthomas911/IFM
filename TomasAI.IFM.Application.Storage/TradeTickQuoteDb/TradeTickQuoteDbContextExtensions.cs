using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;


using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Scylla ingestion bindings, partition-safe batching, validation and provider buffer ownership.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# 14 context extension API keeps the context receiver.")]
internal static class TradeTickQuoteDbContextExtensions
{
    extension(TradeTickQuoteDbContext context)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal Task InsertTickTradeDataAsyncCore(FuturesTickTradeDataInsertedEvent e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var id = e.TickDataId;
            var data = e.TradeData;
            return context.Use($"{nameof(TradeTickQuoteDbCql)}.{nameof(TradeTickQuoteDbCql.InsertTickTradeData)}", TradeTickQuoteDbCql.InsertTickTradeData)
                .SetParameters(new InsertTickTradeData([(sbyte)e.AssetTypeId, id.ContractId, id.ValueDate, TimeOnly.FromDateTime(id.TimestampUtc), id.SequenceId, id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion, e.Dataset, e.DefinitionDate, (int)e.PublisherId, (long)e.InstrumentId, e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn, (long)data.SourceSequence, data.EventTimestampNanoseconds, data.ReceiveTimestampNanoseconds, (short)data.HeaderFlags, data.PriceRaw, data.Price, (long)data.Size, (short)data.Action, (short)data.Side, (short)data.DbnFlags]))
                .ExecuteCommandAsync();
        }

        /// <summary>Writes a bounded quote segment as one native CQL nested-UDT-list value.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "InsertTickQuoteData transfers the encoded value through IScyllaOwnedBindValues.BindLifetime; the Scylla executor disposes it after binding/execution, including failure.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal Task InsertTickQuoteDataAsyncCore(FuturesTickQuoteDataInsertedEvent e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var id = e.TickDataId;
            var encoded = new TickQuoteScyllaBindValue(e.QuoteData);
            object? [] values = [(sbyte)e.AssetTypeId, id.ContractId, id.ValueDate, TimeOnly.FromDateTime(id.TimestampUtc), id.SequenceId, id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion, e.Dataset, e.DefinitionDate, (int)e.PublisherId, (long)e.InstrumentId, e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn, (short)e.EmissionReason, (short)e.QuoteCount, encoded];
            return context.Use($"{nameof(TradeTickQuoteDbCql)}.{nameof(TradeTickQuoteDbCql.InsertTickQuoteData)}", TradeTickQuoteDbCql.InsertTickQuoteData)
                .SetParameters(new InsertTickQuoteData(values, encoded))
                .ExecuteCommandAsync();
        }

        internal Task InsertFuturesTickDataAsyncCore(FuturesTickDataV2ReadModel value)
        {
            context.ValidateTick(value);
            return context.InsertFuturesTickDataAsync(new[] { value });
        }

        internal async Task InsertFuturesTickDataAsyncCore(ICollection<FuturesTickDataV2ReadModel> values)
        {
            context.ValidateTicks(values);
            foreach (var partition in values.GroupBy(row => (row.ContractId, row.ValueDate)))
            {
                foreach (var batch in partition.Chunk(24))
                {
                    List<object> commands = [context.Use(nameof(TradeTickQuoteDbCql.InsertFuturesTickData), TradeTickQuoteDbCql.InsertFuturesTickData)
                .SetParameters(batch.Select(row => new InsertFuturesTickData(row.ContractId, row.ValueDate, row.TickId, row.TickTime, row.Price, row.Size)))
                .QueueCommand(), context.Use(nameof(TradeTickQuoteDbCql.InsertFuturesTickDataByTime), TradeTickQuoteDbCql.InsertFuturesTickDataByTime)
                .SetParameters(batch.Select(row => new InsertFuturesTickDataByTime(row.ContractId, row.ValueDate, row.TickTime, row.TickId, row.Price, row.Size)))
                .QueueCommand()];
                    await context.ExecuteQueuedCommandsAsync(commands, useTransaction: true)
                .ConfigureAwait(false);
                }
            }
        }

        internal Task InsertFuturesOptionTickDataAsyncCore(FuturesOptionTickDataV2ReadModel value)
        {
            context.ValidateOptionTick(value);
            return context.InsertFuturesOptionTickDataAsync(new[] { value });
        }

        internal Task InsertFuturesOptionTickDataAsyncCore(ICollection<FuturesOptionTickDataV2ReadModel> values)
        {
            context.ValidateOptionTicks(values);
            return context.Use(nameof(TradeTickQuoteDbCql.InsertFuturesOptionTickData), TradeTickQuoteDbCql.InsertFuturesOptionTickData)
                .SetParameters(values.Select(row => new InsertFuturesOptionTickData(row.ContractId, row.ValueDate, row.TickId, row.TickTime, row.OptionPrice, row.BidPrice, row.AskPrice, row.BidSize, row.AskSize, row.ImpliedVolatility, row.UnderlyingPrice, row.Delta, row.Gamma, row.Vega, row.Theta, row.Rho)))
                .ExecuteCommandAsync();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal void ValidateScope(string contractId, DateOnly valueDate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
            if (valueDate == default) throw new ArgumentException("A tick value date is required.", nameof(valueDate));
        }
        internal void ValidateTickIdentity([System.Diagnostics.CodeAnalysis.NotNull] FuturesTickDataId? value)
        {
            ArgumentNullException.ThrowIfNull(value);
            context.ValidateScope(value.ContractId, value.ValueDate);
        }
        internal void ValidateOptionTickIdentity([System.Diagnostics.CodeAnalysis.NotNull] FuturesOptionTickDataId? value)
        {
            ArgumentNullException.ThrowIfNull(value);
            context.ValidateScope(value.ContractId, value.ValueDate);
        }
        internal void ValidateFuturesIdentity([System.Diagnostics.CodeAnalysis.NotNull] FuturesDataId? value)
        {
            ArgumentNullException.ThrowIfNull(value);
            context.ValidateScope(value.ContractId, value.ValueDate);
        }
        internal void ValidateTick([System.Diagnostics.CodeAnalysis.NotNull] FuturesTickDataV2ReadModel? value)
        {
            ArgumentNullException.ThrowIfNull(value);
            context.ValidateScope(value.ContractId, value.ValueDate);
        }
        internal void ValidateOptionTick([System.Diagnostics.CodeAnalysis.NotNull] FuturesOptionTickDataV2ReadModel? value)
        {
            ArgumentNullException.ThrowIfNull(value);
            context.ValidateScope(value.ContractId, value.ValueDate);
        }
        internal void ValidateTicks([System.Diagnostics.CodeAnalysis.NotNull] ICollection<FuturesTickDataV2ReadModel>? values)
        {
            ArgumentNullException.ThrowIfNull(values);
            foreach (var value in values) context.ValidateTick(value);
            if (values.Select(row => (row.ContractId, row.ValueDate, row.TickId)).Distinct().Count() != values.Count)
                throw new ArgumentException("Duplicate raw tick identities in one batch.", nameof(values));
        }
        internal void ValidateOptionTicks([System.Diagnostics.CodeAnalysis.NotNull] ICollection<FuturesOptionTickDataV2ReadModel>? values)
        {
            ArgumentNullException.ThrowIfNull(values);
            foreach (var value in values) context.ValidateOptionTick(value);
            if (values.Select(row => (row.ContractId, row.ValueDate, row.TickId)).Distinct().Count() != values.Count)
                throw new ArgumentException("Duplicate raw tick identities in one batch.", nameof(values));
        }
    }
}

internal sealed class TickQuoteScyllaBindValue(FuturesTickQuoteDataSegment segment)
    : ScyllaTickQuoteBindValue(() =>
    {
        var owner = TickQuoteBufferEncoder.EncodePooled(segment);
        return (owner.Buffer, owner);
    });
