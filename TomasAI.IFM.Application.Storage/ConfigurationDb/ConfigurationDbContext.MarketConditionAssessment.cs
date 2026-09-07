using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

public sealed record ResolvedMarketConditionAssessmentParameterSet(MarketConditionAssessmentParameterSet ParameterSet,
    string PayloadSha256, DateTime? EffectiveFromUtc, ConfigurationParameterSetStatus Status);

public partial interface IConfigurationDbContext
{
    Task InsertMarketConditionAssessmentDraftAsync(MarketConditionAssessmentParameterSet parameters, string description, string createdBy, CancellationToken cancellationToken = default);
    Task<ResolvedMarketConditionAssessmentParameterSet?> GetMarketConditionAssessmentAsync(Guid parameterSetId, int version, CancellationToken cancellationToken = default);
    Task<ResolvedMarketConditionAssessmentParameterSet?> ResolveEffectiveMarketConditionAssessmentAsync(DateTime effectiveAtUtc, string marketProfileId, string instrumentRoot, TimeFrameType targetHorizon, CancellationToken cancellationToken = default);
}

public sealed partial class ConfigurationDbContext
{
    const string AssessmentSelect = """
SELECT parameter_set_id, version, schema_version, market_profile_id, instrument_root, target_horizon,
       status, effective_from_utc, payload_json::text, payload_sha256
FROM reference_configuration.market_condition_assessment_parameter_set
""";

    public async Task InsertMarketConditionAssessmentDraftAsync(MarketConditionAssessmentParameterSet parameters,
        string description, string createdBy, CancellationToken cancellationToken = default)
    {
        parameters.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        var normalized = parameters with { Sources = parameters.Sources };
        await dbFactory.ConfigurationDb.Use("AssessmentConfiguration.Insert", """
INSERT INTO reference_configuration.market_condition_assessment_parameter_set
(parameter_set_id,version,schema_version,market_profile_id,instrument_root,target_horizon,status,
 payload_json,payload_sha256,description,created_utc,created_by)
VALUES ($1,$2,$3,$4,$5,$6,0,CAST($7 AS jsonb),$8,$9,$10,$11);
""").SetParameters(new AssessmentDraftParameters(normalized.ParameterSetId, normalized.Version, normalized.SchemaVersion,
            normalized.MarketProfileId, normalized.InstrumentRoot, (short)normalized.TargetHorizon,
            MarketConditionAssessmentHash.Serialize(normalized), MarketConditionAssessmentHash.Parameters(normalized),
            description ?? string.Empty, DateTime.UtcNow, createdBy)).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResolvedMarketConditionAssessmentParameterSet?> GetMarketConditionAssessmentAsync(Guid parameterSetId,
        int version, CancellationToken cancellationToken = default)
    {
        if (parameterSetId == Guid.Empty || version <= 0) throw new ArgumentException("Invalid assessment parameter identity.");
        return await dbFactory.ConfigurationDb.Use("AssessmentConfiguration.Exact", AssessmentSelect + " WHERE parameter_set_id=$1 AND version=$2;")
            .SetParameters(new GetConfiguration(parameterSetId, version)).ExecuteSingleAsync(MapAssessment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResolvedMarketConditionAssessmentParameterSet?> ResolveEffectiveMarketConditionAssessmentAsync(DateTime effectiveAtUtc,
        string marketProfileId, string instrumentRoot, TimeFrameType targetHorizon, CancellationToken cancellationToken = default)
    {
        if (!MarketConditionAssessmentContracts.Utc(effectiveAtUtc) || string.IsNullOrWhiteSpace(marketProfileId) ||
            instrumentRoot != "ES" || !MarketConditionAssessmentParameterSet.IsHorizon(targetHorizon))
            throw new ArgumentException("Invalid assessment configuration selection.");
        var rows = await dbFactory.ConfigurationDb.Use("AssessmentConfiguration.Resolve", AssessmentSelect + """
 WHERE market_profile_id=$1 AND instrument_root=$2 AND target_horizon=$3 AND status=1
 AND effective_from_utc <= $4 AND (retired_at_utc IS NULL OR retired_at_utc > $4)
 ORDER BY effective_from_utc DESC, parameter_set_id, version DESC LIMIT 2;
""").SetParameters(new AssessmentResolveParameters(marketProfileId, instrumentRoot, (short)targetHorizon, effectiveAtUtc))
            .ExecuteQueryAsync(MapAssessment, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 1) throw new InvalidOperationException("Effective assessment profile selection is ambiguous.");
        return rows.FirstOrDefault();
    }

    static ResolvedMarketConditionAssessmentParameterSet MapAssessment(IObjectDataRecord row)
    {
        var p = JsonSerializer.Deserialize<MarketConditionAssessmentParameterSet>(row.GetString(8))
            ?? throw new InvalidOperationException("Invalid stored assessment configuration.");
        p.Validate();
        if (p.ParameterSetId != row.GetGuid(0) || p.Version != row.GetInt(1) || p.SchemaVersion != row.GetInt(2) ||
            p.MarketProfileId != row.GetString(3) || p.InstrumentRoot != row.GetString(4) || (short)p.TargetHorizon != row.GetInt(5) ||
            MarketConditionAssessmentHash.Parameters(p) != row.GetString(9))
            throw new InvalidOperationException("Assessment configuration metadata or payload hash mismatch.");
        return new(p, row.GetString(9), row.IsNull(7) ? null : DateTime.SpecifyKind(row.GetDateTime(7), DateTimeKind.Utc),
            (ConfigurationParameterSetStatus)row.GetInt(6));
    }

    readonly record struct AssessmentDraftParameters(Guid Id, int Version, short Schema, string Profile, string Root, short Horizon,
        string Json, string Hash, string Description, DateTime Created, string CreatedBy) : IBindValue
    { public object Bind() => Values(Uuid(Id), Integer(Version), Smallint(Schema), Text(Profile), Text(Root), Smallint(Horizon), Text(Json), Text(Hash), Text(Description), TimestampTz(Created), Text(CreatedBy)); }
    readonly record struct AssessmentResolveParameters(string Profile, string Root, short Horizon, DateTime At) : IBindValue
    { public object Bind() => Values(Text(Profile), Text(Root), Smallint(Horizon), TimestampTz(At)); }
}
