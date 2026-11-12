using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
/// <summary>
/// Defines asynchronous Configuration database queries.
/// </summary>
public interface IConfigurationDbReadContext
{
    /// <summary>Captures the locked configuration tables, event-log cut, and lookup sequence state.</summary>
    /// <param name="transaction">The caller's enlisted event transaction.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The consistent database image.</returns>
    Task<ConfigurationBaselineDatabaseImage> CaptureConfigurationBaselineAsync(EnlistedEventTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Checks that the table names and order match the supported physical layout.</summary>
    /// <param name="schemaVersion">The baseline schema version.</param>
    /// <param name="tables">The baseline table names in capture order.</param>
    /// <returns>Whether the layout matches.</returns>
    bool MatchesConfigurationBaselineLayout(int schemaVersion, IEnumerable<string> tables);

    /// <summary>Gets one exact strategy position-risk version with validated payload identity and hash.</summary>
    /// <param name="id">The id supplied by the caller.</param>
    /// <param name="version">The exact immutable reference revision.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk.ResolvedStrategyRiskParameterSet?> GetStrategyPositionRiskVersionAsync(Guid id, int version, CancellationToken cancellationToken = default);
    /// <summary>Gets one exact immutable Regime Discovery version.</summary>
    /// <param name = "parameterSetId">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The resolved version, or <see langword="null"/> when it does not exist.</returns>
    Task<ResolvedRegimeDiscoveryParameterSet?> GetRegimeDiscoveryAsync(Guid parameterSetId, int version, CancellationToken cancellationToken = default);
    /// <summary>Gets one exact immutable Market Condition version.</summary>
    /// <param name = "parameterSetId">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The resolved version, or <see langword="null"/> when it does not exist.</returns>
    Task<ResolvedMarketConditionParameterSet?> GetMarketConditionAsync(Guid parameterSetId, int version, CancellationToken cancellationToken = default);
    /// <summary>Gets the effective published Regime Discovery version.</summary>
    /// <param name = "effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name = "targetHorizon">The target horizon.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective version, or <see langword="null"/> when none exists.</returns>
    Task<ResolvedRegimeDiscoveryParameterSet?> GetEffectiveRegimeDiscoveryAsync(DateTime effectiveAtUtc, TimeFrameType targetHorizon, CancellationToken cancellationToken = default);
    /// <summary>Gets the effective published Market Condition version.</summary>
    /// <param name = "effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name = "fundId">The fund identifier.</param>
    /// <param name = "instrumentRoot">The instrument root.</param>
    /// <param name = "targetHorizon">The target horizon.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective version, or <see langword="null"/> when none exists.</returns>
    Task<ResolvedMarketConditionParameterSet?> GetEffectiveMarketConditionAsync(DateTime effectiveAtUtc, int fundId, string instrumentRoot, TimeFrameType targetHorizon, CancellationToken cancellationToken = default);
    /// <summary>Gets lookup definitions for a group.</summary>
    /// <param name = "groupName">The lookup group name.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The ordered lookup definitions.</returns>
    Task<LookupDefinitionReadModel[]> GetLookupDefinitionsAsync(string groupName, CancellationToken cancellationToken = default);
    /// <summary>Gets one exact strategy-catalog definition.</summary>
    /// <param name = "key">The catalog key.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The stored definition, or <see langword="null"/> when it does not exist.</returns>
    Task<StoredStrategyCatalogDefinition?> GetStrategyCatalogAsync(CatalogKey key, CancellationToken cancellationToken = default);
    /// <summary>Gets the latest strategy-catalog versions ordered by stable code.</summary>
    /// <param name = "kind">The catalog kind.</param>
    /// <param name = "limit">The maximum result count.</param>
    /// <param name = "afterCode">The exclusive stable-code cursor.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The strategy-catalog summaries.</returns>
    Task<IReadOnlyList<StrategyCatalogSummary>> GetStrategyCatalogsAsync(StrategyCatalogKind kind, int limit = 50, string? afterCode = null, CancellationToken cancellationToken = default);

    /// <summary>Reads the exact graph and policy rows needed by the catalog owner, in one PostgreSQL snapshot.</summary>
    /// <param name="root">The exact root definition.</param>
    /// <param name="asOfUtc">The effective timestamp.</param>
    /// <param name="allowRootDraft">Allows a Draft root when qualifying publication.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stable set of persisted rows, with the database transaction closed.</returns>
    Task<StrategyCatalogQualificationFacts> ReadStrategyCatalogQualificationAsync(CatalogKey root, DateTime asOfUtc,
        bool allowRootDraft, CancellationToken cancellationToken = default);

    /// <summary>Reads an exact accepted catalog command for idempotent command preparation.</summary>
    /// <param name="stream">The owning catalog stream.</param>
    /// <param name="operationId">The exact command identity.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The accepted source fact, or null if this command has not committed.</returns>
    Task<StrategyCatalogSourceEvent?> ReadCommittedStrategyCatalogOperationAsync(string stream, Guid operationId, CancellationToken token = default);

    /// <summary>Gets one exact volatility-series definition.</summary>
    /// <param name = "identity">The definition identity.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The resolved definition, or <see langword="null"/> when it does not exist.</returns>
    Task<ResolvedVolatilitySeriesDefinition?> GetVolatilitySeriesDefinitionAsync(VolatilitySeriesIdentity identity, CancellationToken cancellationToken = default);
    /// <summary>Gets the effective volatility-series definition.</summary>
    /// <param name = "environment">The market-data environment.</param>
    /// <param name = "seriesId">The series identifier.</param>
    /// <param name = "effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective definition, or <see langword="null"/> when none exists.</returns>
    Task<ResolvedVolatilitySeriesDefinition?> GetEffectiveVolatilitySeriesDefinitionAsync(string environment, string seriesId, DateTimeOffset effectiveAtUtc, CancellationToken cancellationToken = default);
    /// <summary>Gets an effective Trade Selection activation.</summary>
    /// <param name = "id">The activation identifier.</param>
    /// <param name = "version">The activation version.</param>
    /// <param name = "hash">The expected payload digest.</param>
    /// <param name = "effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective activation.</returns>
    Task<TradeSelectionActivation> GetEffectiveTradeSelectionActivationAsync(Guid id, int version, string hash, DateTime effectiveAtUtc, CancellationToken cancellationToken = default);
    /// <summary>Gets one exact Trade Selection parameter-set version.</summary>
    /// <param name = "id">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The resolved version, or <see langword="null"/> when it does not exist.</returns>
    Task<ResolvedTradeSelectionParameterSet?> GetTradeSelectionVersionAsync(Guid id, int version, CancellationToken cancellationToken = default);
    /// <summary>Gets an effective Trade Selection parameter-set version.</summary>
    /// <param name = "id">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "hash">The expected payload digest.</param>
    /// <param name = "effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective parameter-set version.</returns>
    Task<ResolvedTradeSelectionParameterSet> GetEffectiveTradeSelectionVersionAsync(Guid id, int version, string hash, DateTime effectiveAtUtc, CancellationToken cancellationToken = default);
    /// <summary>Gets one exact pipeline policy snapshot.</summary>
    /// <param name = "kind">The pipeline parameter kind.</param>
    /// <param name = "id">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The policy snapshot, or <see langword="null"/> when it does not exist.</returns>
    Task<SelectionPipelinePolicySnapshot?> GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind kind, Guid id, int version, CancellationToken cancellationToken = default);
    /// <summary>Gets one exact Market Condition Assessment parameter set.</summary>
    /// <param name = "parameterSetId">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The resolved parameter set, or <see langword="null"/> when it does not exist.</returns>
    Task<ResolvedMarketConditionAssessmentParameterSet?> GetMarketConditionAssessmentAsync(Guid parameterSetId, int version, CancellationToken cancellationToken = default);
    /// <summary>Gets the effective Market Condition Assessment parameter set.</summary>
    /// <param name = "effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name = "marketProfileId">The market-profile identifier.</param>
    /// <param name = "instrumentRoot">The instrument root.</param>
    /// <param name = "targetHorizon">The target horizon.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective parameter set, or <see langword="null"/> when none exists.</returns>
    Task<ResolvedMarketConditionAssessmentParameterSet?> GetEffectiveMarketConditionAssessmentAsync(DateTime effectiveAtUtc, string marketProfileId, string instrumentRoot, TimeFrameType targetHorizon, CancellationToken cancellationToken = default);
    /// <summary>Gets legacy parameter versions.</summary>
    /// <param name = "setId">The optional legacy set identifier.</param>
    /// <param name = "version">The optional exact version, or zero for all versions.</param>
    /// <param name = "offset">The result offset.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The legacy parameter versions.</returns>
    Task<ParameterLegacyVersion[]> GetLegacyParameterVersionsAsync(Guid? setId = null, int version = 0, int offset = 0, CancellationToken cancellationToken = default);
    /// <summary>Gets registered parameter components.</summary>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The registered parameter components.</returns>
    Task<ParameterComponentSummary[]> GetParameterComponentsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets a parameter schema definition.</summary>
    /// <param name = "componentCode">The component code.</param>
    /// <param name = "version">The schema version.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The schema definition, or <see langword="null"/> when it does not exist.</returns>
    Task<ParameterSchemaDefinition?> GetParameterSchemaAsync(string componentCode, int version, CancellationToken cancellationToken = default);
    /// <summary>Gets parameter-set versions using stable cursor pagination.</summary>
    /// <param name = "componentCode">The component code.</param>
    /// <param name = "setId">The optional parameter-set identifier.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <param name = "limit">The maximum result count.</param>
    /// <param name = "afterName">The exclusive name cursor.</param>
    /// <param name = "afterSetId">The exclusive set-identifier cursor.</param>
    /// <param name = "afterVersion">The exclusive version cursor.</param>
    /// <returns>The parameter-set versions.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    Task<ParameterSetVersion[]> GetParameterSetsAsync(string componentCode, Guid? setId = null, CancellationToken cancellationToken = default, int limit = 100, string afterName = "", Guid? afterSetId = null, int afterVersion = 0);

    /// <summary>Lists active startup run identities in stable creation order, independently of actor caches.</summary>
    /// <param name="limit">The maximum number of returned rows.</param>
    /// <param name="afterCreatedAtUtc">The after created at utc supplied by the caller.</param>
    /// <param name="afterRunId">The after run id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<Guid[]> GetParameterStartupRunIdsAsync(int limit, DateTime? afterCreatedAtUtc = null, Guid? afterRunId = null, CancellationToken cancellationToken = default);

    /// <summary>Reads active parameter startup from the committed projection.</summary>
    /// <param name="runId">The run id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<ParameterStartupReadModel> GetActiveParameterStartupAsync(
        Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Reads parameter set snapshot from the committed projection.</summary>
    /// <param name="setId">The set id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ParameterSetSnapshot> GetParameterSetSnapshotAsync(Guid setId, CancellationToken cancellationToken = default);

    /// <summary>Reads parameter assignment snapshot from the committed projection.</summary>
    /// <param name="identity">The identity supplied by the caller.</param>
    /// <param name="scope">The exact owning application scope.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ParameterAssignmentSnapshot> GetParameterAssignmentSnapshotAsync(
        ParameterAssignmentEntityId identity, ParameterAssignmentScope scope, CancellationToken cancellationToken = default);

    /// <summary>Reads parameter startup inputs from the committed projection.</summary>
    /// <param name="assignmentIds">The assignment ids supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ParameterStartupInputsReadModel> GetParameterStartupInputsAsync(
        Guid[] assignmentIds, CancellationToken cancellationToken = default);

    /// <summary>Has parameter startup projection.</summary>
    /// <param name="runId">The run id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>True when the requested condition holds.</returns>
    Task<bool> HasParameterStartupProjectionAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Reads active parameter startup page from the committed projection.</summary>
    /// <param name="limit">The maximum number of returned rows.</param>
    /// <param name="afterCreatedAtUtc">The after created at utc supplied by the caller.</param>
    /// <param name="afterRunId">The after run id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ParameterStartupRun[]> GetActiveParameterStartupPageAsync(
        int limit, DateTime? afterCreatedAtUtc, Guid? afterRunId, CancellationToken cancellationToken = default);

    /// <summary>Reads the concurrency fence for prepared parameter-command evidence.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<long> GetParameterRevisionFenceAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the accepted revision when all accepted parameter facts have been projected.</summary>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<long?> GetProjectedParameterRevisionAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads typed scalar/child rows in the caller's transaction and verifies their content fingerprint.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="setId">The set id supplied by the caller.</param>
    /// <param name="version">The exact immutable reference revision.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<ParameterSetValue?> GetTypedParameterValueAsync(IEnlistedPostgresTransaction transaction, Guid setId, int version, CancellationToken cancellationToken = default);

    /// <summary>Reads and verifies one immutable assignment projection revision.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="assignmentId">The assignment id supplied by the caller.</param>
    /// <param name="revision">The revision supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<ParameterAssignmentRevision?> GetParameterAssignmentProjectionAsync(IEnlistedPostgresTransaction transaction, Guid assignmentId, long revision, CancellationToken cancellationToken = default);
    /// <summary>Reads and verifies the typed audit projection for an accepted operation.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="operationId">The operation id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<ParameterAuditEntry?> GetParameterAuditProjectionAsync(IEnlistedPostgresTransaction transaction, Guid operationId, CancellationToken cancellationToken = default);
    /// <summary>Reads a frozen startup projection and its exact typed parameter versions.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="runId">The run id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<ParameterStartupRun?> GetParameterStartupProjectionAsync(IEnlistedPostgresTransaction transaction, Guid runId, CancellationToken cancellationToken = default);
    /// <summary>Reads and verifies a startup preparation report projection.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="runId">The run id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    Task<ParameterSignalStartupReport?> GetParameterStartupReportProjectionAsync(IEnlistedPostgresTransaction transaction, Guid runId, CancellationToken cancellationToken = default);
}
