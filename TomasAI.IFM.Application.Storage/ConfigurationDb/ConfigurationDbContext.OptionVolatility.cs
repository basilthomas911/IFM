using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

public sealed record ResolvedVolatilitySeriesDefinition(
    VolatilitySeriesDefinition Definition,
    string PayloadSha256);

public partial interface IConfigurationDbContext
{
    Task InsertVolatilitySeriesDefinitionAsync(VolatilitySeriesDefinition definition,
        CancellationToken cancellationToken = default);
    Task<ResolvedVolatilitySeriesDefinition?> GetVolatilitySeriesDefinitionAsync(
        VolatilitySeriesIdentity identity, CancellationToken cancellationToken = default);
    Task<ResolvedVolatilitySeriesDefinition?> ResolveVolatilitySeriesDefinitionAsync(
        string environment, string seriesId, DateTimeOffset effectiveAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed partial class ConfigurationDbContext
{
    const string VolatilitySeriesSelect = """
SELECT series_id,methodology_version,environment,effective_from_utc,effective_until_utc,
       payload_json::text,payload_sha256
FROM reference_configuration.volatility_series_definition
""";

    public async Task InsertVolatilitySeriesDefinitionAsync(VolatilitySeriesDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ValidateVolatilityDefinition(definition);
        var json = JsonSerializer.Serialize(definition);
        var hash = Sha256(json);
        await dbFactory.ConfigurationDb.Use("VolatilitySeriesDefinition.Insert", """
INSERT INTO reference_configuration.volatility_series_definition
(series_id,methodology_version,environment,effective_from_utc,effective_until_utc,payload_json,payload_sha256,
 approved_configuration_version,owner,approval_evidence_id)
VALUES ($1,$2,$3,$4,$5,CAST($6 AS jsonb),$7,$8,$9,$10)
ON CONFLICT (series_id,methodology_version) DO NOTHING;
""").SetParameters(new VolatilityDefinitionParameters(definition.Identity.SeriesId,
            definition.Identity.MethodologyVersion, definition.DataIdentity.Environment,
            definition.Governance.EffectiveFromUtc.UtcDateTime,
            definition.Governance.EffectiveUntilUtc?.UtcDateTime, json, hash,
            definition.Governance.ApprovedConfigurationVersion, definition.Governance.Owner,
            definition.Governance.ApprovalEvidenceId)).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

        var stored = await GetVolatilitySeriesDefinitionAsync(definition.Identity, cancellationToken).ConfigureAwait(false);
        if (stored is null || stored.PayloadSha256 != hash)
            throw new InvalidOperationException("Volatility series identity conflicts with different immutable content.");
    }

    public Task<ResolvedVolatilitySeriesDefinition?> GetVolatilitySeriesDefinitionAsync(
        VolatilitySeriesIdentity identity, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(identity);
        return dbFactory.ConfigurationDb.Use("VolatilitySeriesDefinition.Exact", VolatilitySeriesSelect +
                " WHERE series_id=$1 AND methodology_version=$2;")
            .SetParameters(new VolatilityIdentityParameters(identity.SeriesId, identity.MethodologyVersion))
            .ExecuteSingleAsync(MapVolatilityDefinition, cancellationToken);
    }

    public async Task<ResolvedVolatilitySeriesDefinition?> ResolveVolatilitySeriesDefinitionAsync(
        string environment, string seriesId, DateTimeOffset effectiveAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesId);
        if (effectiveAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("UTC effective time is required.");
        var rows = await dbFactory.ConfigurationDb.Use("VolatilitySeriesDefinition.Resolve", VolatilitySeriesSelect + """
 WHERE environment=$1 AND series_id=$2 AND effective_from_utc <= $3
 AND (effective_until_utc IS NULL OR effective_until_utc > $3)
 ORDER BY effective_from_utc DESC,methodology_version DESC LIMIT 2;
""").SetParameters(new VolatilityResolveParameters(environment, seriesId, effectiveAtUtc.UtcDateTime))
            .ExecuteQueryAsync(MapVolatilityDefinition, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 1) throw new InvalidOperationException("Effective volatility series selection is ambiguous.");
        return rows.FirstOrDefault();
    }

    static ResolvedVolatilitySeriesDefinition MapVolatilityDefinition(IObjectDataRecord row)
    {
        var definition = JsonSerializer.Deserialize<VolatilitySeriesDefinition>(row.GetString(5))
            ?? throw new InvalidDataException("Stored volatility series payload is missing.");
        ValidateVolatilityDefinition(definition);
        var hash = Sha256(JsonSerializer.Serialize(definition));
        if (definition.Identity.SeriesId != row.GetString(0) ||
            definition.Identity.MethodologyVersion != row.GetString(1) ||
            definition.DataIdentity.Environment != row.GetString(2) || hash != row.GetString(6))
            throw new InvalidDataException("Stored volatility series identity or digest is invalid.");
        return new(definition, hash);
    }

    static void ValidateVolatilityDefinition(VolatilitySeriesDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateIdentity(definition.Identity);
        if (definition.SchemaVersion != VolatilitySeriesDefinition.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(definition.DataIdentity.Environment) ||
            string.IsNullOrWhiteSpace(definition.Governance.ApprovedConfigurationVersion) ||
            string.IsNullOrWhiteSpace(definition.Governance.Owner) ||
            string.IsNullOrWhiteSpace(definition.Governance.ApprovalEvidenceId) ||
            definition.Governance.EffectiveFromUtc.Offset != TimeSpan.Zero ||
            definition.Governance.EffectiveUntilUtc is { } until && until.Offset != TimeSpan.Zero ||
            definition.Governance.EffectiveUntilUtc <= definition.Governance.EffectiveFromUtc ||
            definition.Construction.IntradayCoalescingInterval <= TimeSpan.Zero ||
            definition.Construction.MaximumIntradayCheckpointsPerValueDate <= 0)
            throw new ArgumentException("A complete approved volatility series definition is required.", nameof(definition));
    }

    static void ValidateIdentity(VolatilitySeriesIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.SeriesId);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.MethodologyVersion);
    }

    static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    readonly record struct VolatilityDefinitionParameters(string SeriesId, string MethodologyVersion,
        string Environment, DateTime EffectiveFrom, DateTime? EffectiveUntil, string Json, string Hash,
        string ApprovedVersion, string Owner, string Evidence) : IBindValue
    {
        public object Bind() => Values(Text(SeriesId), Text(MethodologyVersion), Text(Environment),
            TimestampTz(EffectiveFrom), TimestampTz(EffectiveUntil),
            Text(Json), Text(Hash), Text(ApprovedVersion), Text(Owner), Text(Evidence));
    }
    readonly record struct VolatilityIdentityParameters(string SeriesId, string MethodologyVersion) : IBindValue
    { public object Bind() => Values(Text(SeriesId), Text(MethodologyVersion)); }
    readonly record struct VolatilityResolveParameters(string Environment, string SeriesId, DateTime At) : IBindValue
    { public object Bind() => Values(Text(Environment), Text(SeriesId), TimestampTz(At)); }
}
