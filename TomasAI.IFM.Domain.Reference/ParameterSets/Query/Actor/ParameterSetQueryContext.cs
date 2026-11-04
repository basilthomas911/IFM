using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Application.Storage;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;

/// <summary>Provides the closed-generic strategy-configuration Query context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class ParameterSetQueryContext
    : QueryActorContext,
      IParameterSetQueryContext
{
    readonly Lazy<IRegimeDiscoveryMarketSignalSnapshotProvider> signalSnapshots;
    public IRegimeDiscoveryMarketSignalSnapshotProvider SignalSnapshots => signalSnapshots.Value;
    readonly Lazy<IParameterAccessPolicy> accessPolicy;
    public IParameterAccessPolicy AccessPolicy => accessPolicy.Value;

    readonly Lazy<IConfigurationDbContext> configurationDb;

    /// <summary>Initializes the Query context.</summary>
    public ParameterSetQueryContext(
        IActorSupervisor supervisor,
        ILogger<ParameterSetQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, ParameterSetQueryActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger);
        signalSnapshots = new(() => IsArgumentNull.Set(Container.Resolve<IRegimeDiscoveryMarketSignalSnapshotProvider>())!);
        accessPolicy = new(() => IsArgumentNull.Set(Container.Resolve<IParameterAccessPolicy>())!);
        configurationDb = new(() => IsArgumentNull.Set(Container.Resolve<IConfigurationDbContext>())!);
    }

    /// <inheritdoc />
    public IConfigurationDbContext ConfigurationDb => configurationDb.Value;
    /// <inheritdoc />
    public ILogger<ParameterSetQueryActor> Logger { get; }
}
