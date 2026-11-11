using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
/// <summary>
/// Defines asynchronous Configuration database commands.
/// </summary>
public interface IConfigurationDbWriteContext
{
    /// <summary>Invalidates prepared evidence in the same transaction as an accepted parameter mutation.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task AdvanceParameterRevisionFenceAsync(IEnlistedPostgresTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Mark parameter projection pending.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="eventId">The event id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task MarkParameterProjectionPendingAsync(IEnlistedPostgresTransaction transaction, long eventId, CancellationToken cancellationToken = default);

    /// <summary>Complete parameter projection.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="eventId">The event id supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>True when the requested condition holds.</returns>
    Task<bool> CompleteParameterProjectionAsync(IEnlistedPostgresTransaction transaction, long eventId, CancellationToken cancellationToken = default);

    /// <summary>Creates a validated immutable strategy position-risk draft; activation remains explicit.</summary>
    /// <param name="policy">The policy supplied by the caller.</param>
    /// <param name="description">The description supplied by the caller.</param>
    /// <param name="createdBy">The originating user or service identity.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertStrategyPositionRiskDraftAsync(TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk.StrategyRiskParameterSet policy, string description, string createdBy, CancellationToken cancellationToken = default);
    /// <summary>Inserts an immutable Regime Discovery draft.</summary>
    /// <param name = "parameterSet">The validated parameter set.</param><param name = "description">The description.</param><param name = "createdBy">The author.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertRegimeDiscoveryDraftAsync(RegimeDiscoveryParameterSet parameterSet, string description, string createdBy, CancellationToken cancellationToken = default);
    /// <summary>Materializes an accepted Regime Discovery draft with its original creation timestamp.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="parameterSet">The parameter set supplied by the caller.</param>
    /// <param name="description">The description supplied by the caller.</param>
    /// <param name="createdBy">The originating user or service identity.</param>
    /// <param name="createdAtUtc">The created at utc supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertRegimeDiscoveryDraftAsync(IEnlistedPostgresTransaction transaction, RegimeDiscoveryParameterSet parameterSet, string description, string createdBy, DateTime createdAtUtc, CancellationToken cancellationToken = default);
    /// <summary>Inserts an immutable Market Condition draft.</summary>
    /// <param name = "parameterSet">The validated parameter set.</param><param name = "description">The description.</param><param name = "createdBy">The author.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertMarketConditionDraftAsync(MarketConditionParameterSet parameterSet, string description, string createdBy, CancellationToken cancellationToken = default);
    /// <summary>Publishes an immutable parameter-set version.</summary>
    /// <param name = "kind">The parameter-set kind.</param><param name = "parameterSetId">The parameter-set identifier.</param><param name = "version">The version.</param><param name = "effectiveFromUtc">The UTC effective timestamp.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task PublishAsync(StrategyParameterSetKind kind, Guid parameterSetId, int version, DateTime effectiveFromUtc, CancellationToken cancellationToken = default);
    /// <summary>Retires a published parameter-set version.</summary>
    /// <param name = "kind">The parameter-set kind.</param><param name = "parameterSetId">The parameter-set identifier.</param><param name = "version">The version.</param><param name = "retiredAtUtc">The UTC retirement timestamp.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the retirement operation.</returns>
    Task RetireAsync(StrategyParameterSetKind kind, Guid parameterSetId, int version, DateTime retiredAtUtc, CancellationToken cancellationToken = default);
    /// <summary>Inserts an immutable strategy-catalog draft and its normalized children.</summary>
    /// <param name = "definition">The validated definition.</param><param name = "expectedPreviousVersion">The expected previous version.</param><param name = "createdBy">The author.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The canonical content digest.</returns>
    Task<string> InsertStrategyCatalogDraftAsync(StrategyCatalogDefinition definition, int expectedPreviousVersion, string createdBy, CancellationToken cancellationToken = default);

    /// <param name="stream">The exact source stream selected by the catalog owner.</param>
    /// <summary>Prepare strategy catalog transition.</summary>
    /// <param name="enlisted">The enlisted supplied by the caller.</param>
    /// <param name="request">The caller's typed selection, including its filtering and page bounds.</param>
    /// <param name="principal">The principal supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The selected row, or null when no matching published row exists.</returns>
    /// <param name="publication">The owner-qualified publication proof for a new publish operation.</param>
    Task<PreparedStrategyCatalogTransition?> PrepareStrategyCatalogTransitionAsync(EnlistedEventTransaction enlisted, CatalogCommandRequest request,
        string principal, string stream, StrategyCatalogPublicationProof? publication = null, CancellationToken token = default);

    /// <summary>Project strategy catalog source.</summary>
    /// <param name="enlisted">The enlisted supplied by the caller.</param>
    /// <param name="source">The source supplied by the caller.</param>
    /// <param name="rebuild">The rebuild supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>True when the requested condition holds.</returns>
    Task<bool> ProjectStrategyCatalogSourceAsync(EnlistedEventTransaction enlisted, StrategyCatalogSourceEvent source,
        bool rebuild, CancellationToken token = default);

    /// <summary>Capture strategy catalog baseline.</summary>
    /// <param name="enlisted">The enlisted supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<IReadOnlyList<CatalogKey>> ReadLockedStrategyCatalogBaselineKeysAsync(EnlistedEventTransaction enlisted, CancellationToken token = default);

    /// <summary>Reads an exact catalog projection under the caller's transaction and row lock.</summary>
    /// <param name="enlisted">The caller-owned transaction.</param>
    /// <param name="key">The exact catalog identity.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The locked projection, or null if absent.</returns>
    Task<StoredStrategyCatalogDefinition?> GetLockedStrategyCatalogAsync(EnlistedEventTransaction enlisted, CatalogKey key, CancellationToken token = default);

    /// <summary>Reads strategy catalog source heads from the committed projection.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<IReadOnlyList<StrategyCatalogSourceEvent>> GetStrategyCatalogSourceHeadsAsync(
        IEnlistedPostgresTransaction transaction, CancellationToken token = default);


    /// <summary>Publishes a prepared draft only if its qualified graph and policy rows still match.</summary>
    /// <param name="proof">The exact graph fingerprint prepared by the catalog owner.</param>
    /// <param name="expectedContentHash">The expected root content digest.</param>
    /// <param name="publishedBy">The accepting principal.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Completion of the fenced publication.</returns>
    Task PublishPreparedStrategyCatalogAsync(StrategyCatalogPublicationProof proof, string expectedContentHash,
        string publishedBy, CancellationToken cancellationToken = default);
    /// <summary>Retires a published strategy-catalog version.</summary>
    /// <param name = "key">The catalog key.</param><param name = "expectedContentHash">The expected content digest.</param><param name = "retiredAtUtc">The UTC retirement timestamp.</param><param name = "retiredBy">The retiring user.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the retirement operation.</returns>
    Task RetireStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime retiredAtUtc, string retiredBy, CancellationToken cancellationToken = default);
    /// <summary>Inserts an immutable Risk Management draft.</summary>
    /// <param name = "policy">The validated policy.</param><param name = "description">The description.</param><param name = "createdBy">The author.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertRiskManagementDraftAsync(RiskParameterSet policy, string description, string createdBy, CancellationToken cancellationToken = default);
    /// <summary>Inserts an immutable volatility-series definition.</summary>
    /// <param name = "definition">The validated definition.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertVolatilitySeriesDefinitionAsync(VolatilitySeriesDefinition definition, CancellationToken cancellationToken = default);
    /// <summary>Inserts an immutable order-composition draft.</summary>
    /// <param name = "policy">The validated policy.</param><param name = "description">The description.</param><param name = "createdBy">The author.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertSelectionConstructionDraftAsync(SelectionConstructionPolicy policy, string description, string createdBy, CancellationToken cancellationToken = default);
    /// <summary>Inserts an immutable Trade Selection activation draft.</summary>
    /// <param name = "activation">The validated activation.</param><param name = "description">The description.</param><param name = "createdBy">The author.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertTradeSelectionActivationDraftAsync(TradeSelectionActivation activation, string description, string createdBy, CancellationToken cancellationToken = default);
    /// <summary>Inserts an immutable Trade Selection draft.</summary>
    /// <param name = "parameters">The validated parameter set.</param><param name = "description">The description.</param><param name = "createdBy">The author.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertTradeSelectionDraftAsync(TradeSelectionParameterSet parameters, string description, string createdBy, CancellationToken cancellationToken = default);
    /// <summary>Inserts an immutable Market Condition Assessment draft.</summary>
    /// <param name = "parameters">The validated parameter set.</param><param name = "description">The description.</param><param name = "createdBy">The author.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertMarketConditionAssessmentDraftAsync(MarketConditionAssessmentParameterSet parameters, string description, string createdBy, CancellationToken cancellationToken = default);
    /// <summary>Projects a parameter-set fact.</summary>
    /// <param name = "fact">The parameter-set fact.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the projection operation.</returns>
    Task ProjectParameterSetAsync(IParameterSetFact fact, CancellationToken cancellationToken = default);
    /// <summary>Materializes an accepted parameter set fact on the caller-owned source-event transaction.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="fact">The fact supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectParameterSetAsync(IEnlistedPostgresTransaction transaction, IParameterSetFact fact, CancellationToken cancellationToken = default);
    /// <summary>Projects a parameter-assignment fact.</summary>
    /// <param name = "fact">The assignment fact.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the projection operation.</returns>
    Task ProjectParameterAssignmentAsync(ParameterAssignmentChangedEvent fact, CancellationToken cancellationToken = default);
    /// <summary>Materializes an accepted parameter assignment fact on the caller-owned source-event transaction.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="fact">The fact supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectParameterAssignmentAsync(IEnlistedPostgresTransaction transaction, ParameterAssignmentChangedEvent fact, CancellationToken cancellationToken = default);
    /// <summary>Projects a parameter-startup fact.</summary>
    /// <param name = "fact">The startup fact.</param><param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the projection operation.</returns>
    Task ProjectParameterStartupAsync(ParameterStartupChangedEvent fact, CancellationToken cancellationToken = default);
    /// <summary>Materializes an accepted parameter startup fact on the caller-owned source-event transaction.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="fact">The fact supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectParameterStartupAsync(IEnlistedPostgresTransaction transaction, ParameterStartupChangedEvent fact, CancellationToken cancellationToken = default);
    /// <summary>Acquires the cross-host parameter write lease.</summary>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The asynchronous lease handle.</returns>
    Task<IAsyncDisposable> AcquireParameterWriteLeaseAsync(CancellationToken cancellationToken = default);
    /// <summary>Materializes the accepted publication of a Regime Discovery draft.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="parameterSetId">The parameter set id supplied by the caller.</param>
    /// <param name="version">The exact immutable reference revision.</param>
    /// <param name="effectiveFromUtc">The effective from utc supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task PublishRegimeDiscoveryAsync(IEnlistedPostgresTransaction transaction, Guid parameterSetId, int version, DateTime effectiveFromUtc, CancellationToken cancellationToken = default);
    /// <summary>Materializes the accepted retirement of a Regime Discovery version.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="parameterSetId">The parameter set id supplied by the caller.</param>
    /// <param name="version">The exact immutable reference revision.</param>
    /// <param name="retiredAtUtc">The retired at utc supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task RetireRegimeDiscoveryAsync(IEnlistedPostgresTransaction transaction, Guid parameterSetId, int version, DateTime retiredAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Inserts immutable typed parameter rows in the source event transaction; identical replays are idempotent.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="value">The accepted value supplied by the caller.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task InsertTypedParameterValueAsync(IEnlistedPostgresTransaction transaction, ParameterSetValue value, CancellationToken cancellationToken = default);
}
