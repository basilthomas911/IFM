using System.Runtime.CompilerServices;
using Cassandra;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Bounded same-partition unlogged row batches. All keys and causation come from the durable original payload.</summary>
public sealed class RawFuturesRowBatchStore
{
    static readonly ConditionalWeakTable<ISession, Task<RawFuturesRowBatchStore>> Stores = new();
    readonly ISession session;
    readonly PreparedStatement trade, quote;
    RawFuturesRowBatchStore(ISession session, PreparedStatement trade, PreparedStatement quote)
    { this.session = session; this.trade = trade; this.quote = quote; }
    public static async Task<RawFuturesRowBatchStore> GetAsync(ISession session)
    {
        try { return await Stores.GetValue(session, PrepareAsync).ConfigureAwait(false); }
        catch { Stores.Remove(session); throw; }
    }
    public static async Task<RawFuturesRowBatchStore> PrepareAsync(ISession session) => new(session,
        await session.PrepareAsync(TradeTickQuoteDbSql.InsertTickTradeData).ConfigureAwait(false),
        await session.PrepareAsync(TradeTickQuoteDbSql.InsertTickQuoteData).ConfigureAwait(false));
    static LocalDate Date(DateOnly date) => new(date.Year, date.Month, date.Day);
    // Match the existing TimeOnly provider's millisecond clustering key. Original
    // 100 ns observation ticks and exchange nanoseconds remain in their own columns.
    static LocalTime Time(DateTime at) => new(at.TimeOfDay.Ticks / TimeSpan.TicksPerMillisecond * 1_000_000);
    static object[] Common(FuturesTickTradeDataChangedEvent e)
    {
        var id = e.TickDataId; var d = e.TradeData;
        return [(sbyte)e.AssetTypeId, id.ContractId, Date(id.ValueDate), Time(id.TimestampUtc), id.SequenceId,
            id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion, e.Dataset, Date(e.DefinitionDate), (int)e.PublisherId,
            (long)e.InstrumentId, e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn,
            (long)d.SourceSequence, d.EventTimestampNanoseconds, d.ReceiveTimestampNanoseconds, (short)d.HeaderFlags,
            d.PriceRaw, d.Price, (long)d.Size, (short)d.Action, (short)d.Side, (short)d.DbnFlags];
    }
    public Task WriteAsync(RawFuturesStorageBatch batch, CancellationToken token = default) => WriteCoreAsync(batch, true, token);
    internal Task WriteIndividuallyAsync(RawFuturesStorageBatch batch, CancellationToken token = default) => WriteCoreAsync(batch, false, token);
    async Task WriteCoreAsync(RawFuturesStorageBatch batch, bool grouped, CancellationToken token)
    {
        if (batch.SourceEpoch == Guid.Empty || batch.Records.Length is < 1 or > RawFuturesStorageBatch.MaximumCount)
            throw new InvalidDataException("Invalid durable raw storage batch.");
        var groups = new Dictionary<(string Table, AssetTypeId Asset, string Contract), BatchStatement>();
        var owners = new List<IDisposable>();
        var ordered = new List<BoundStatement>(batch.Records.Length);
        try
        {
            long previous = batch.Records[0].Ordinal - 1;
            foreach (var record in batch.Records)
            {
                token.ThrowIfCancellationRequested();
                if (record.Ordinal != previous + 1 || record.Payload.Length > 16_384) throw new InvalidDataException("Raw storage ordinal gap or oversized payload.");
                previous = record.Ordinal;
                BoundStatement command; (string, AssetTypeId, string) key;
                if (record.Kind == 1)
                {
                    var e = MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(record.Payload, cancellationToken: token);
                    if (e.AssetTypeId != AssetTypeId.Futures || e.OptionMarketPriceObservation is not null) throw new InvalidDataException("Raw source is not a futures execution.");
                    command = trade.Bind(Common(e)); key = ("trade", e.AssetTypeId, e.TickDataId.ContractId);
                }
                else if (record.Kind == 2)
                {
                    var e = MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(record.Payload, cancellationToken: token);
                    owners.Add(e.QuoteData);
                    if (e.AssetTypeId != AssetTypeId.Futures || e.QuoteCount == 0) throw new InvalidDataException("Invalid raw futures quote.");
                    var encoded = new TickQuoteScyllaBindValue(e.QuoteData); owners.Add(encoded);
                    var value = encoded.Resolve(session, quote); var id = e.TickDataId;
                    command = quote.Bind((sbyte)e.AssetTypeId, id.ContractId, Date(id.ValueDate), Time(id.TimestampUtc),
                        id.SequenceId, id.TimestampUtc, id.TimestampUtc.Ticks, (short)e.SchemaVersion, e.Dataset, Date(e.DefinitionDate),
                        (int)e.PublisherId, (long)e.InstrumentId, e.Id, e.EventId, e.CommandId, e.AggregateId, e.EventSource, e.ReceivedOn,
                        (short)e.EmissionReason, (short)e.QuoteCount, value);
                    key = ("quote", e.AssetTypeId, id.ContractId);
                }
                else throw new InvalidDataException("Unknown raw source record kind.");
                if (!groups.TryGetValue(key, out var group)) groups.Add(key, group = new BatchStatement().SetBatchType(BatchType.Unlogged));
                group.Add(command);
                ordered.Add(command);
            }
            var commands = grouped ? groups.Values.Cast<IStatement>() : ordered.Cast<IStatement>();
            foreach (var group in commands)
            {
                token.ThrowIfCancellationRequested();
                // Join actual driver completion before returning pooled quote buffers, even if cancelled.
                using var result = await session.ExecuteAsync(group).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
            }
        }
        finally { foreach (var owner in owners) owner.Dispose(); }
    }
}
