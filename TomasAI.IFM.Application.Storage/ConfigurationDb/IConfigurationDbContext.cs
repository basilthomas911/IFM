using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>Defines immutable PostgreSQL strategy-configuration lifecycle operations.</summary>
public interface IConfigurationDbContext : IObjectRepository<ConfigurationDbContext>
{
    /// <summary>Inserts one immutable Draft Regime Discovery version after hash and contract validation.</summary>
    Task InsertRegimeDiscoveryDraftAsync(
        RegimeDiscoveryParameterSet parameterSet,
        string description,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>Publishes one immutable version with an explicit effective timestamp.</summary>
    Task PublishAsync(
        StrategyParameterSetKind kind,
        Guid parameterSetId,
        int version,
        DateTime effectiveFromUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Retires one published version for future workflow selection.</summary>
    Task RetireAsync(
        StrategyParameterSetKind kind,
        Guid parameterSetId,
        int version,
        DateTime retiredAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Gets one exact immutable Regime Discovery version.</summary>
    Task<ResolvedRegimeDiscoveryParameterSet?> GetRegimeDiscoveryAsync(
        Guid parameterSetId,
        int version,
        CancellationToken cancellationToken = default);

    /// <summary>Deterministically resolves the single effective published Regime Discovery version.</summary>
    Task<ResolvedRegimeDiscoveryParameterSet?> ResolveEffectiveRegimeDiscoveryAsync(
        DateTime effectiveAtUtc,
        TimeFrameType targetHorizon,
        CancellationToken cancellationToken = default);
}
