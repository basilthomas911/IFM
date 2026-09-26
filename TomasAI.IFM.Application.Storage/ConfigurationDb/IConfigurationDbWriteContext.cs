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
    /// <summary>Inserts an immutable Regime Discovery draft.</summary>
    /// <param name="parameterSet">The validated parameter set.</param><param name="description">The description.</param><param name="createdBy">The author.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertRegimeDiscoveryDraftAsync(RegimeDiscoveryParameterSet parameterSet, string description, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>Inserts an immutable Market Condition draft.</summary>
    /// <param name="parameterSet">The validated parameter set.</param><param name="description">The description.</param><param name="createdBy">The author.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertMarketConditionDraftAsync(MarketConditionParameterSet parameterSet, string description, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>Publishes an immutable parameter-set version.</summary>
    /// <param name="kind">The parameter-set kind.</param><param name="parameterSetId">The parameter-set identifier.</param><param name="version">The version.</param><param name="effectiveFromUtc">The UTC effective timestamp.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task PublishAsync(StrategyParameterSetKind kind, Guid parameterSetId, int version, DateTime effectiveFromUtc, CancellationToken cancellationToken = default);

    /// <summary>Retires a published parameter-set version.</summary>
    /// <param name="kind">The parameter-set kind.</param><param name="parameterSetId">The parameter-set identifier.</param><param name="version">The version.</param><param name="retiredAtUtc">The UTC retirement timestamp.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the retirement operation.</returns>
    Task RetireAsync(StrategyParameterSetKind kind, Guid parameterSetId, int version, DateTime retiredAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Inserts an immutable strategy-catalog draft and its normalized children.</summary>
    /// <param name="definition">The validated definition.</param><param name="expectedPreviousVersion">The expected previous version.</param><param name="createdBy">The author.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>The canonical content digest.</returns>
    Task<string> InsertStrategyCatalogDraftAsync(StrategyCatalogDefinition definition, int expectedPreviousVersion, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>Publishes a strategy-catalog draft.</summary>
    /// <param name="key">The catalog key.</param><param name="expectedContentHash">The expected content digest.</param><param name="effectiveFromUtc">The UTC effective timestamp.</param><param name="publishedBy">The publisher.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    Task PublishStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime effectiveFromUtc, string publishedBy, CancellationToken cancellationToken = default);

    /// <summary>Retires a published strategy-catalog version.</summary>
    /// <param name="key">The catalog key.</param><param name="expectedContentHash">The expected content digest.</param><param name="retiredAtUtc">The UTC retirement timestamp.</param><param name="retiredBy">The retiring user.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the retirement operation.</returns>
    Task RetireStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime retiredAtUtc, string retiredBy, CancellationToken cancellationToken = default);

    /// <summary>Inserts an immutable Risk Management draft.</summary>
    /// <param name="policy">The validated policy.</param><param name="description">The description.</param><param name="createdBy">The author.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertRiskManagementDraftAsync(RiskParameterSet policy, string description, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>Inserts an immutable volatility-series definition.</summary>
    /// <param name="definition">The validated definition.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertVolatilitySeriesDefinitionAsync(VolatilitySeriesDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Inserts an immutable order-composition draft.</summary>
    /// <param name="policy">The validated policy.</param><param name="description">The description.</param><param name="createdBy">The author.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertSelectionConstructionDraftAsync(SelectionConstructionPolicy policy, string description, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>Inserts an immutable Trade Selection activation draft.</summary>
    /// <param name="activation">The validated activation.</param><param name="description">The description.</param><param name="createdBy">The author.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertTradeSelectionActivationDraftAsync(TradeSelectionActivation activation, string description, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>Inserts an immutable Trade Selection draft.</summary>
    /// <param name="parameters">The validated parameter set.</param><param name="description">The description.</param><param name="createdBy">The author.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertTradeSelectionDraftAsync(TradeSelectionParameterSet parameters, string description, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>Inserts an immutable Market Condition Assessment draft.</summary>
    /// <param name="parameters">The validated parameter set.</param><param name="description">The description.</param><param name="createdBy">The author.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    Task InsertMarketConditionAssessmentDraftAsync(MarketConditionAssessmentParameterSet parameters, string description, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>Projects a parameter-set fact.</summary>
    /// <param name="fact">The parameter-set fact.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the projection operation.</returns>
    Task ProjectParameterSetAsync(IParameterSetFact fact, CancellationToken cancellationToken = default);

    /// <summary>Projects a parameter-assignment fact.</summary>
    /// <param name="fact">The assignment fact.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the projection operation.</returns>
    Task ProjectParameterAssignmentAsync(ParameterAssignmentChangedEvent fact, CancellationToken cancellationToken = default);

    /// <summary>Projects a parameter-startup fact.</summary>
    /// <param name="fact">The startup fact.</param><param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the projection operation.</returns>
    Task ProjectParameterStartupAsync(ParameterStartupChangedEvent fact, CancellationToken cancellationToken = default);

    /// <summary>Acquires the cross-host parameter write lease.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The asynchronous lease handle.</returns>
    Task<IAsyncDisposable> AcquireParameterWriteLeaseAsync(CancellationToken cancellationToken = default);
}
