using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;

/// <summary>Provides the closed-generic strategy-configuration Query context.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class ParameterStartupQueryContext
    : QueryActorContext,
      IParameterStartupQueryContext
{
    readonly Lazy<IRegimeDiscoveryMarketSignalSnapshotProvider> signalSnapshots;
    public IRegimeDiscoveryMarketSignalSnapshotProvider SignalSnapshots => signalSnapshots.Value;
    readonly Lazy<IParameterAccessPolicy> accessPolicy;
    public IParameterAccessPolicy AccessPolicy => accessPolicy.Value;

    readonly Lazy<IConfigurationDbContext> configurationDb;

    /// <summary>Initializes the Query context.</summary>
    public ParameterStartupQueryContext(
        IActorSupervisor supervisor,
        ILogger<ParameterStartupQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, ParameterStartupQueryActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger);
        signalSnapshots = new(() => IsArgumentNull.Set(Container.Resolve<IRegimeDiscoveryMarketSignalSnapshotProvider>())!);
        accessPolicy = new(() => IsArgumentNull.Set(Container.Resolve<IParameterAccessPolicy>())!);
        configurationDb = new(() => IsArgumentNull.Set(Container.Resolve<IConfigurationDbContext>())!);
    }

    /// <inheritdoc />
    public IConfigurationDbContext ConfigurationDb => configurationDb.Value;
    /// <inheritdoc />
    public ILogger<ParameterStartupQueryActor> Logger { get; }
}
