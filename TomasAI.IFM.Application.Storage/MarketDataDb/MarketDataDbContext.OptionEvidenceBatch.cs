using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    readonly record struct EvidenceKey(string Contract, DateOnly Date, string Identity)
    {
        public static EvidenceKey From(OptionTradeSource source) => new(source.ContractId, source.ValueDate, source.Identity);
        public static EvidenceKey Read(IObjectDataRecord row) => new(row.GetString(0), row.GetDateOnly(1), row.GetString(2));
    }

    /// <inheritdoc />
    public async ValueTask<bool[]> WriteFirstBatchAsync(IReadOnlyList<OptionTradeEvidence> evidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Count is < 1 or > IOptionTradeEvidenceWriter.MaximumFirstWriteBatch)
            throw new ArgumentOutOfRangeException(nameof(evidence));
        if (evidence.Count == 1)
            return [await WriteFirstAsync(evidence[0], cancellationToken).ConfigureAwait(false)];

        var serializer = MessagePackBinarySerializer.Shared;
        var requests = new (EvidenceKey Key, string Digest, byte[] Payload)[evidence.Count];
        for (var index = 0; index < evidence.Count; index++)
        {
            var item = evidence[index];
            ArgumentNullException.ThrowIfNull(item);
            item.Validate();
            if (MessagePackBinarySerializer.MeasureContent(item) > 131072)
                throw new ArgumentException("Trade evidence exceeds its bounded payload.", nameof(evidence));
            requests[index] = (EvidenceKey.From(item.Source), item.Source.SourceDigest, serializer.Serialize(item)!);
        }

        // Same-key requests within a window follow the first submitted attempt.
        // The database statement orders different keys before taking write locks.
        var first = requests.DistinctBy(static request => request.Key).ToArray();
        var accepted = await Database.Use("OptionTradeEvidence.InsertBatch", MarketDataDbSql.InsertOptionTradeEvidenceBatch)
            .SetParameters((object)BindValues.Values(
                first.Select(static r => r.Key.Contract).ToArray(),
                first.Select(static r => r.Key.Date).ToArray(),
                first.Select(static r => r.Key.Identity).ToArray(),
                first.Select(static r => r.Digest).ToArray(),
                first.Select(static r => r.Payload).ToArray()))
            .ExecuteQueryAsync(EvidenceKey.Read, cancellationToken).ConfigureAwait(false);

        var inserted = accepted.ToHashSet();
        var durable = first.Where(r => inserted.Contains(r.Key)).ToDictionary(static r => r.Key, static r => r.Payload);
        var conflicts = first.Where(r => !inserted.Contains(r.Key)).Select(static r => r.Key).ToArray();
        if (conflicts.Length != 0)
        {
            // A concurrent insert can win the unique-key lock after the insertion
            // statement's snapshot. Read conflicts in a separate command.
            var stored = await Database.Use("OptionTradeEvidence.ReadConflicts", MarketDataDbSql.GetOptionTradeEvidenceBatch)
                .SetParameters((object)BindValues.Values(
                    conflicts.Select(static k => k.Contract).ToArray(),
                    conflicts.Select(static k => k.Date).ToArray(),
                    conflicts.Select(static k => k.Identity).ToArray()))
                .ExecuteQueryAsync(static row =>
                {
                    var key = EvidenceKey.Read(row);
                    return (Key: key, Value: ReadOptionTradeEvidence(key, row.GetString(3), row.GetBytes(4)));
                }, cancellationToken).ConfigureAwait(false);
            foreach (var row in stored)
                durable.Add(row.Key, serializer.Serialize(row.Value)!);
            if (stored.Count != conflicts.Length)
                throw new InvalidOperationException("Durable option trade evidence write not confirmed.");
        }

        return requests.Select(r => durable[r.Key].AsSpan().SequenceEqual(r.Payload)).ToArray();
    }

    static OptionTradeEvidence ReadOptionTradeEvidence(EvidenceKey key, string digest, byte[] payload)
    {
        if (payload.Length is 0 or > 131072)
            throw new InvalidDataException("Invalid trade evidence payload size.");
        var value = MessagePackBinarySerializer.Shared.Deserialize<OptionTradeEvidence>(payload)
            ?? throw new InvalidDataException("Missing trade evidence.");
        value.Validate();
        if (EvidenceKey.From(value.Source) != key || !string.Equals(value.Source.SourceDigest, digest, StringComparison.Ordinal))
            throw new InvalidDataException("Trade evidence identity mismatch.");
        return value;
    }
}
