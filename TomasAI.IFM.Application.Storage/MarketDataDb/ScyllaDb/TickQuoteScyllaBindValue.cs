using Cassandra;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.Storage.ScyllaDb;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>
/// Validates and binds the encoded tick-quote value to its Scylla prepared marker.
/// </summary>
internal sealed class TickQuoteScyllaBindValue(FuturesTickQuoteDataSegment segment)
    : IScyllaPreparedBindValue, IDisposable
{
    PooledTickQuoteBuffer? _owner;
    int _resolved;
    static readonly object Gate = new();
    static readonly object ValidatedMarker = new();
    static readonly ConditionalWeakTable<PreparedStatement, object> ValidatedStatements = new();
    static readonly (string Name, ColumnTypeCode Type)[] ExpectedFields =
    [
        ("source_sequence", ColumnTypeCode.Bigint),
        ("source_event_timestamp_ns", ColumnTypeCode.Bigint),
        ("source_receive_timestamp_ns", ColumnTypeCode.Bigint),
        ("header_flags", ColumnTypeCode.SmallInt),
        ("bid_price_raw", ColumnTypeCode.Bigint),
        ("bid_price", ColumnTypeCode.Decimal),
        ("bid_size", ColumnTypeCode.Bigint),
        ("bid_count", ColumnTypeCode.Bigint),
        ("ask_price_raw", ColumnTypeCode.Bigint),
        ("ask_price", ColumnTypeCode.Decimal),
        ("ask_size", ColumnTypeCode.Bigint),
        ("ask_count", ColumnTypeCode.Bigint)
    ];

    /// <summary>
    /// Checks the prepared marker once and returns one owned encoded value.
    /// </summary>
    public object Resolve(ISession session, PreparedStatement statement)
    {
        if (session.BinaryProtocolVersion < 3)
            throw new InvalidOperationException("Native CQL quote-list encoding requires protocol version 3 or later.");
        if (!ValidatedStatements.TryGetValue(statement, out _))
        {
            lock (Gate)
            {
                if (!ValidatedStatements.TryGetValue(statement, out _))
                {
                    Validate(session, statement);
                    ValidatedStatements.Add(statement, ValidatedMarker);
                }
            }
        }
        if (Interlocked.Exchange(ref _resolved, 1) != 0)
            throw new InvalidOperationException("The quote CQL value has already been resolved.");
        var owner = TickQuoteBufferEncoder.EncodePooled(segment);
        Volatile.Write(ref _owner, owner);
        return owner.Buffer;
    }

    /// <summary>
    /// Returns the encoded bytes after the Scylla request ends.
    /// </summary>
    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Dispose();

    static void Validate(ISession session, PreparedStatement statement)
    {
        var columns = statement.Variables.Columns;
        if (columns.Length != 21 || columns[20].Name != "quote_data"
            || columns[20].TypeCode != ColumnTypeCode.List
            || columns[20].TypeInfo is not ListColumnInfo list
            || list.ValueTypeCode != ColumnTypeCode.Udt
            || list.ValueTypeInfo is not UdtColumnInfo udt
            || udt.Name != session.Keyspace + ".tick_quote_item"
            || udt.Fields.Count != ExpectedFields.Length)
            throw new InvalidOperationException("Prepared quote_data marker is not the expected frozen list of tick_quote_item UDT values.");

        for (var index = 0; index < ExpectedFields.Length; index++)
        {
            var field = udt.Fields[index];
            var expected = ExpectedFields[index];
            if (field.Name != expected.Name || field.TypeCode != expected.Type)
                throw new InvalidOperationException(
                    $"tick_quote_item field {index} is {field.Name}/{field.TypeCode}; expected {expected.Name}/{expected.Type}.");
        }
    }
}
