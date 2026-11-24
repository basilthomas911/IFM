using System.Buffers.Binary;
using System.Security.Cryptography;
using Cassandra;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Prepared immutable chunk writes and exact source-time reads. Does not own its shared session.</summary>
public sealed class TradeBatchScyllaStore
{
    readonly ISession session;
    readonly PreparedStatement manifest, getManifest, insert, getChunk, range;
    TradeBatchScyllaStore(ISession session, PreparedStatement[] statements)
    { this.session = session; manifest = statements[0]; getManifest = statements[1]; insert = statements[2]; getChunk = statements[3]; range = statements[4]; }
    public static async Task<TradeBatchScyllaStore> PrepareAsync(ISession session)
    {
        var statements = new List<PreparedStatement>();
        foreach (var sql in new[] { TradeBatchScyllaSchema.InsertManifest, TradeBatchScyllaSchema.ReadManifest,
                     TradeBatchScyllaSchema.InsertChunk, TradeBatchScyllaSchema.ReadChunk, TradeBatchScyllaSchema.ReadRange })
            statements.Add(await session.PrepareAsync(sql).ConfigureAwait(false));
        ValidateMarker(session, statements[2]);
        return new(session, statements.ToArray());
    }
    static void ValidateMarker(ISession session, PreparedStatement statement)
    {
        var columns = statement.Variables.Columns;
        if (session.BinaryProtocolVersion < 3 || columns.Length != 18 || columns[17].Name != "trade_data"
            || columns[17].TypeCode != ColumnTypeCode.List || columns[17].TypeInfo is not ListColumnInfo list
            || list.ValueTypeCode != ColumnTypeCode.Udt || list.ValueTypeInfo is not UdtColumnInfo udt
            || udt.Name != session.Keyspace + ".tick_trade_item_v1")
            throw new InvalidDataException("Prepared raw trade list does not match its binary codec.");
        (string Name, ColumnTypeCode Type)[] expected =
        [
            ("source_sequence", ColumnTypeCode.Bigint), ("event_ns", ColumnTypeCode.Bigint), ("receive_ns", ColumnTypeCode.Bigint),
            ("header_flags", ColumnTypeCode.SmallInt), ("price_raw", ColumnTypeCode.Bigint), ("price", ColumnTypeCode.Decimal),
            ("size", ColumnTypeCode.Bigint), ("action", ColumnTypeCode.SmallInt), ("side", ColumnTypeCode.SmallInt),
            ("dbn_flags", ColumnTypeCode.SmallInt), ("sequence_id", ColumnTypeCode.Bigint), ("aggregation_ticks", ColumnTypeCode.Bigint),
            ("event_id", ColumnTypeCode.Uuid), ("command_id", ColumnTypeCode.Uuid), ("event_log_id", ColumnTypeCode.Bigint),
            ("aggregate_id", ColumnTypeCode.Varchar), ("event_source", ColumnTypeCode.Varchar), ("received_ticks", ColumnTypeCode.Bigint),
            ("acceptance_ordinal", ColumnTypeCode.Bigint)
        ];
        if (udt.Fields.Count != expected.Length) throw new InvalidDataException("Unexpected raw trade UDT field count.");
        for (var index = 0; index < expected.Length; index++)
            if (udt.Fields[index].Name != expected[index].Name || udt.Fields[index].TypeCode != expected[index].Type)
                throw new InvalidDataException($"Raw trade UDT field {index} does not match its binary codec.");
    }
    static LocalDate Date(DateOnly date) => new(date.Year, date.Month, date.Day);
    static long Second(in TradeStorageRecord record) => record.Trade.EventTimestampNanoseconds / 1_000_000_000;
    static int End(TradeStorageSegment records, int start)
    {
        var end = start + 1; var second = Second(records.Items[start]);
        while (end < records.Count && Second(records.Items[end]) == second) end++;
        return end;
    }
    public async Task WriteAsync(TradeStorageBatch batch, CancellationToken token = default)
    {
        if (batch.Version != TradeStorageBatch.CurrentVersion || batch.BatchId == Guid.Empty || batch.SourceEpoch == Guid.Empty
            || string.IsNullOrWhiteSpace(batch.ContractId) || string.IsNullOrWhiteSpace(batch.Dataset)
            || batch.Records.Count is < 1 or > TradeStorageSegment.MaximumCount)
            throw new InvalidDataException("Invalid raw-trade batch identity.");
        using var binary = new TradeBatchBinaryBuffer();
        MessagePackSerializer.Serialize(binary, batch, cancellationToken: token);
        var hash = SHA256.HashData(binary.WrittenSpan);
        var chunks = 0;
        for (var start = 0; start < batch.Records.Count; start = End(batch.Records, start)) chunks++;
        using (var result = await ExecuteAsync(manifest.Bind((sbyte)batch.AssetType, batch.ContractId, Date(batch.ValueDate),
                   batch.BatchId, hash, chunks, batch.Dataset, Date(batch.DefinitionDate), (int)batch.PublisherId,
                   (long)batch.InstrumentId, batch.SourceEpoch), token).ConfigureAwait(false))
        {
            if (!result.Single().GetValue<bool>("[applied]"))
            {
                using var existing = await ExecuteAsync(getManifest.Bind((sbyte)batch.AssetType, batch.ContractId, Date(batch.ValueDate), batch.BatchId), token).ConfigureAwait(false);
                var row = existing.Single();
                if (!hash.AsSpan().SequenceEqual(row.GetValue<byte[]>("content_hash")) || row.GetValue<int>("chunk_count") != chunks)
                    throw new InvalidDataException("Trade batch identity conflicts with its persisted manifest.");
            }
        }
        var chunkIndex = 0;
        for (var start = 0; start < batch.Records.Count;)
        {
            var end = End(batch.Records, start);
            // Borrow the segment while its transport owner remains alive for the whole write.
            var records = batch.Records.Slice(start, end - start);
            using var encoded = TickTradeBufferEncoder.EncodePooled(records);
            var second = Second(records.Items[0]);
            var hour = checked((int)(second / 3600));
            var chunkId = ChunkId(batch.BatchId, chunkIndex);
            var firstNs = records.Items[0].Trade.EventTimestampNanoseconds;
            var lastNs = records.Items[^1].Trade.EventTimestampNanoseconds;
            foreach (var record in records.Items)
            {
                firstNs = Math.Min(firstNs, record.Trade.EventTimestampNanoseconds);
                lastNs = Math.Max(lastNs, record.Trade.EventTimestampNanoseconds);
            }
            var command = insert.Bind((sbyte)batch.AssetType, batch.ContractId, Date(batch.ValueDate), hour, second, chunkId,
                batch.BatchId, chunkIndex, hash, records.Count, firstNs, lastNs, batch.Dataset, Date(batch.DefinitionDate),
                (int)batch.PublisherId, (long)batch.InstrumentId, batch.SourceEpoch, encoded.Buffer);
            using var applied = await ExecuteAsync(command, token).ConfigureAwait(false);
            if (!applied.Single().GetValue<bool>("[applied]"))
            {
                using var existing = await ExecuteAsync(getChunk.Bind((sbyte)batch.AssetType, batch.ContractId, Date(batch.ValueDate), hour, second, chunkId), token).ConfigureAwait(false);
                if (!hash.AsSpan().SequenceEqual(existing.Single().GetValue<byte[]>("content_hash")))
                    throw new InvalidDataException("Trade chunk conflicts with its persisted manifest.");
            }
            start = end; chunkIndex++;
        }
    }

    // Cancellation cannot release encoded buffers while the driver can still reference them.
    async Task<RowSet> ExecuteAsync(IStatement command, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var result = await session.ExecuteAsync(command).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        return result;
    }
    static Guid ChunkId(Guid batchId, int index)
    {
        Span<byte> identity = stackalloc byte[20]; batchId.TryWriteBytes(identity);
        BinaryPrimitives.WriteInt32BigEndian(identity[16..], index);
        Span<byte> digest = stackalloc byte[32]; SHA256.HashData(identity, digest);
        return new Guid(digest[..16]);
    }

    /// <summary>Returns original records in an absolute half-open nanosecond range, preserving acceptance order within each epoch.</summary>
    public async Task<IReadOnlyList<TradeStorageRecord>> ReadAsync(AssetTypeId asset, string contract, DateOnly valueDate,
        long fromNanoseconds, long untilNanoseconds, CancellationToken token = default)
    {
        if (untilNanoseconds <= fromNanoseconds) return [];
        var values = new List<TradeStorageRecord>();
        var firstHour = checked((int)(fromNanoseconds / 3_600_000_000_000));
        var lastHour = checked((int)((untilNanoseconds - 1) / 3_600_000_000_000));
        if (lastHour - firstHour > 24) throw new ArgumentOutOfRangeException(nameof(untilNanoseconds));
        for (var hour = firstHour; hour <= lastHour; hour++)
        {
            var command = range.Bind((sbyte)asset, contract, Date(valueDate), hour,
                fromNanoseconds / 1_000_000_000, (untilNanoseconds - 1) / 1_000_000_000).SetPageSize(1);
            using var rows = await ExecuteAsync(command, token).ConfigureAwait(false);
            foreach (var row in rows)
            {
                token.ThrowIfCancellationRequested();
                var items = row.GetValue<IEnumerable<byte[]>>("trade_data");
                var count = row.GetValue<int>("record_count");
                var decodedCount = 0;
                foreach (var item in items)
                {
                    var value = TickTradeBufferEncoder.DecodeItem(item);
                    decodedCount++;
                    if (value.Trade.EventTimestampNanoseconds >= fromNanoseconds && value.Trade.EventTimestampNanoseconds < untilNanoseconds)
                        values.Add(value);
                }
                if (decodedCount != count) throw new InvalidDataException("Trade chunk count mismatch.");
            }
        }
        values.Sort(static (left, right) =>
        {
            var time = left.Trade.EventTimestampNanoseconds.CompareTo(right.Trade.EventTimestampNanoseconds);
            return time != 0 ? time : left.AcceptanceOrdinal.CompareTo(right.AcceptanceOrdinal);
        });
        return values;
    }
}
