using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Idempotent first-attempt evidence. Daily contract partitions; no quote replay queue or implicit retention TTL.</summary>
public sealed class OptionTradeEvidenceStore(IObjectRepository db) : IOptionTradeEvidenceWriter
{
    public const string CreateTable = """
        CREATE TABLE IF NOT EXISTS option_trade_evidence(
            contract_id text, value_date date, source_id text, source_digest text, payload blob,
            PRIMARY KEY ((contract_id,value_date),source_id));
        """;
    public async ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
    {
        evidence.Validate();
        if (MessagePackBinarySerializer.MeasureContent(evidence) > 131072) throw new ArgumentException("Trade evidence exceeds its bounded payload.");
        var source = evidence.Source;
        await db.Use("OptionTradeEvidence.Insert", """
            INSERT INTO option_trade_evidence(contract_id,value_date,source_id,source_digest,payload)
            VALUES(:contract,:date,:id,:digest,:payload) IF NOT EXISTS;
            """).SetParameters(new Values([source.ContractId, source.ValueDate, source.Identity, source.SourceDigest,
                MessagePackBinarySerializer.Shared.Serialize(evidence)!])).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        var stored = await ReadAsync(source.ContractId, source.ValueDate, source.Identity, cancellationToken).ConfigureAwait(false);
        if (stored is null || stored.Source.SourceDigest != source.SourceDigest)
            throw new InvalidOperationException("Trade source identity collision or durable write not confirmed.");
    }
    public async Task<OptionTradeEvidence?> ReadAsync(string contract, DateOnly date, string sourceId, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(contract) || contract.Length > 128 || date == default || sourceId.Length != 64)
            throw new ArgumentException("Exact trade source identity is required.");
        var rows = await db.Use("OptionTradeEvidence.Read", """
            SELECT source_digest,payload FROM option_trade_evidence WHERE contract_id=:contract AND value_date=:date AND source_id=:id;
            """).SetParameters(new Values([contract, date, sourceId])).ExecuteQueryAsync(row =>
        {
            var payload = row.GetBytes(1);
            if (payload.Length is 0 or > 131072) throw new InvalidDataException("Invalid trade evidence payload size.");
            var value = MessagePackBinarySerializer.Shared.Deserialize<OptionTradeEvidence>(payload)
                ?? throw new InvalidDataException("Missing trade evidence.");
            value.Validate();
            if (value.Source.ContractId != contract || value.Source.ValueDate != date || value.Source.Identity != sourceId
                || value.Source.SourceDigest != row.GetString(0))
                throw new InvalidDataException("Trade evidence identity mismatch.");
            return value;
        }, token).ConfigureAwait(false);
        return rows.SingleOrDefault();
    }
    readonly record struct Values(object[] Items) : IBindValue { public object Bind() => Items; }
}
