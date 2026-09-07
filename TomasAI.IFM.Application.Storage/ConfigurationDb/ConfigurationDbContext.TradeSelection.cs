using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

public sealed record ResolvedTradeSelectionParameterSet(TradeSelectionParameterSet ParameterSet, string PayloadSha256,
    ConfigurationParameterSetStatus Status, DateTime? EffectiveFromUtc, DateTime? RetiredAtUtc);

public partial interface IConfigurationDbContext
{
    Task InsertSelectionConstructionDraftAsync(SelectionConstructionPolicy policy, string description, string createdBy, CancellationToken cancellationToken = default);
    Task InsertTradeSelectionActivationDraftAsync(TradeSelectionActivation activation, string description, string createdBy, CancellationToken cancellationToken = default);
    Task<TradeSelectionActivation> ResolveTradeSelectionActivationAsync(Guid id, int version, string hash, DateTime at, CancellationToken cancellationToken = default);
    Task InsertTradeSelectionDraftAsync(TradeSelectionParameterSet parameters, string description, string createdBy, CancellationToken cancellationToken = default);
    Task<ResolvedTradeSelectionParameterSet?> GetTradeSelectionVersionAsync(Guid id, int version, CancellationToken cancellationToken = default);
    Task<ResolvedTradeSelectionParameterSet> ResolveTradeSelectionVersionAsync(Guid id, int version, string hash, DateTime at, CancellationToken cancellationToken = default);
    Task<SelectionPipelinePolicySnapshot?> GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind kind, Guid id, int version, CancellationToken cancellationToken = default);
}

public sealed partial class ConfigurationDbContext
{
    public async Task InsertSelectionConstructionDraftAsync(SelectionConstructionPolicy policy, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        policy.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        await dbFactory.ConfigurationDb.Use("SelectionConstruction.Insert", SelectionInsert.Replace("trade_selection_parameter_set", "order_composition_parameter_set"))
            .SetParameters(new InsertConfigurationDraft(policy.ParameterSetId, policy.Version, policy.SchemaVersion, 0, policy.Serialize(), policy.Hash(), description, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task InsertTradeSelectionActivationDraftAsync(TradeSelectionActivation activation, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        activation.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        await dbFactory.ConfigurationDb.Use("SelectionActivation.Insert", SelectionInsert.Replace("trade_selection_parameter_set", "intrinsic_time_strategy_workflow_parameter_set"))
            .SetParameters(new InsertConfigurationDraft(activation.ParameterSetId, activation.Version, activation.SchemaVersion, 0, activation.Serialize(), activation.Hash(), description, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<TradeSelectionActivation> ResolveTradeSelectionActivationAsync(Guid id, int version, string hash, DateTime at, CancellationToken cancellationToken = default)
    {
        var row = await GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow, id, version, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Exact workflow activation is missing.");
        var activation = TradeSelectionActivation.Read(row.PayloadJson);
        if (activation.ParameterSetId != id || activation.Version != version || activation.Hash() != hash || row.PayloadSha256 != hash || row.Status != CatalogLifecycleStatus.Published || row.EffectiveFromUtc is null || row.EffectiveFromUtc > at || row.RetiredAtUtc <= at)
            throw new InvalidOperationException("Exact published workflow activation is invalid or unavailable.");
        var selected = await ResolveTradeSelectionVersionAsync(activation.SelectionPolicyReference.Id, activation.SelectionPolicyReference.Version, activation.SelectionPolicyReference.PayloadSha256, at, cancellationToken).ConfigureAwait(false);
        if (selected.ParameterSet.TargetHorizon != activation.TargetHorizon || selected.ParameterSet.InstrumentRoot != activation.InstrumentRoot) throw new InvalidOperationException("Activation selector scope mismatch.");
        return activation;
    }

    public async Task InsertTradeSelectionDraftAsync(TradeSelectionParameterSet parameters, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        TradeSelectionPolicy.Validate(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        await dbFactory.ConfigurationDb.Use("TradeSelectionConfiguration.Insert", SelectionInsert)
            .SetParameters(new InsertConfigurationDraft(parameters.ParameterSetId, parameters.Version, parameters.SchemaVersion, 0,
                TradeSelectionPolicy.Serialize(parameters), TradeSelectionPolicy.Hash(parameters), description ?? "", DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task<ResolvedTradeSelectionParameterSet?> GetTradeSelectionVersionAsync(Guid id, int version, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || version <= 0) throw new ArgumentException("Invalid selection policy identity.");
        return await dbFactory.ConfigurationDb.Use("TradeSelectionConfiguration.Exact", SelectionSelect)
            .SetParameters(new GetConfiguration(id, version)).ExecuteSingleAsync(row =>
            {
                var p = TradeSelectionPolicy.Read(row.GetString(6));
                if (p.ParameterSetId != row.GetGuid(0) || p.Version != row.GetInt(1) || p.SchemaVersion != row.GetInt(2) || TradeSelectionPolicy.Hash(p) != row.GetString(7))
                    throw new InvalidOperationException("TS.CONTRACT.HASH: Stored policy metadata/hash mismatch.");
                return new ResolvedTradeSelectionParameterSet(p, row.GetString(7), (ConfigurationParameterSetStatus)row.GetInt(3),
                    row.IsNull(4) ? null : DateTime.SpecifyKind(row.GetDateTime(4), DateTimeKind.Utc), row.IsNull(5) ? null : DateTime.SpecifyKind(row.GetDateTime(5), DateTimeKind.Utc));
            }, cancellationToken).ConfigureAwait(false);
    }
    public async Task<ResolvedTradeSelectionParameterSet> ResolveTradeSelectionVersionAsync(Guid id, int version, string hash, DateTime at, CancellationToken cancellationToken = default)
    {
        var row = await GetTradeSelectionVersionAsync(id, version, cancellationToken).ConfigureAwait(false);
        if (!TradeSelectionContracts.Utc(at) || row is null || row.PayloadSha256 != hash || row.Status != ConfigurationParameterSetStatus.Published || row.EffectiveFromUtc > at || row.EffectiveFromUtc is null || row.RetiredAtUtc <= at)
            throw new InvalidOperationException("TS.CONFIG.MISSING: Exact effective published selector policy is required.");
        return row;
    }
    public async Task<SelectionPipelinePolicySnapshot?> GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind kind, Guid id, int version, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || version <= 0) throw new ArgumentException("Invalid pipeline policy identity.");
        var table = kind switch
        {
            CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow => "intrinsic_time_strategy_workflow_parameter_set",
            CatalogPipelineParameterKind.RegimeDiscovery => "regime_discovery_parameter_set",
            CatalogPipelineParameterKind.MarketCondition => "market_condition_parameter_set",
            CatalogPipelineParameterKind.MarketConditionAssessment => "market_condition_assessment_parameter_set",
            CatalogPipelineParameterKind.TradeSelection => "trade_selection_parameter_set",
            CatalogPipelineParameterKind.OrderComposition => "order_composition_parameter_set",
            CatalogPipelineParameterKind.RiskManagement => "risk_management_parameter_set",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var sql = "SELECT parameter_set_id,version,schema_version,status,effective_from_utc,retired_at_utc,payload_json::text,payload_sha256 FROM reference_configuration." + table + " WHERE parameter_set_id=$1 AND version=$2;";
        return await dbFactory.ConfigurationDb.Use("SelectionPipeline.Exact." + kind, sql).SetParameters(new GetConfiguration(id, version))
            .ExecuteSingleAsync(row => new SelectionPipelinePolicySnapshot
            {
                Kind = kind,
                Id = row.GetGuid(0),
                Version = row.GetInt(1),
                SchemaVersion = checked((short)row.GetInt(2)),
                Status = (CatalogLifecycleStatus)row.GetInt(3),
                EffectiveFromUtc = row.IsNull(4) ? null : DateTime.SpecifyKind(row.GetDateTime(4), DateTimeKind.Utc),
                RetiredAtUtc = row.IsNull(5) ? null : DateTime.SpecifyKind(row.GetDateTime(5), DateTimeKind.Utc),
                PayloadJson = row.GetString(6),
                PayloadSha256 = row.GetString(7)
            }, cancellationToken).ConfigureAwait(false);
    }
    const string SelectionInsert = """
INSERT INTO reference_configuration.trade_selection_parameter_set
(parameter_set_id,version,schema_version,status,payload_json,payload_sha256,description,created_utc,created_by)
VALUES ($1,$2,$3,$4,CAST($5 AS jsonb),$6,$7,$8,$9);
""";
    const string SelectionSelect = """
SELECT parameter_set_id,version,schema_version,status,effective_from_utc,retired_at_utc,payload_json::text,payload_sha256
FROM reference_configuration.trade_selection_parameter_set WHERE parameter_set_id=$1 AND version=$2;
""";
}
