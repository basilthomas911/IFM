using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;

/// <summary>Defines readonly services owned by the strategy-configuration Query actor.</summary>
public interface IParameterSetQueryContext
    : IQueryActorContext<ParameterSetQueryActor>
{
    IRegimeDiscoveryMarketSignalSnapshotProvider SignalSnapshots{get;}
    IParameterAccessPolicy AccessPolicy { get; }
    IEventSourceActorStateRepository<ParameterStartupCommandState> Startups {get;}
    /// <summary>Gets ConfigurationDb.</summary>
    IConfigurationDbContext ConfigurationDb { get; }
    IEventSourceActorStateRepository<ParameterSetCommandState> StateRepository {get;}
    IEventSourceActorStateRepository<ParameterAssignmentCommandState> Assignments {get;}
    /// <summary>Gets the logger.</summary>
    ILogger<ParameterSetQueryActor> Logger { get; }
}

/// <summary>Provides the closed-generic strategy-configuration Query context.</summary>
public sealed class ParameterSetQueryContext
    : QueryActorContext,
      IQueryActorContext<ParameterSetQueryActor>,
      IParameterSetQueryContext
{
    readonly Lazy<IEventSourceActorStateRepository<ParameterStartupCommandState>> startups;
    public IEventSourceActorStateRepository<ParameterStartupCommandState> Startups=>startups.Value;
    readonly Lazy<IRegimeDiscoveryMarketSignalSnapshotProvider> signalSnapshots;
    public IRegimeDiscoveryMarketSignalSnapshotProvider SignalSnapshots=>signalSnapshots.Value;
    readonly Lazy<IParameterAccessPolicy> accessPolicy;
    public IParameterAccessPolicy AccessPolicy => accessPolicy.Value;

    readonly Lazy<IEventSourceActorStateRepository<ParameterAssignmentCommandState>> assignments;
    readonly Lazy<IConfigurationDbContext> configurationDb;
    readonly Lazy<IEventSourceActorStateRepository<ParameterSetCommandState>> repository;

    /// <summary>Initializes the Query context.</summary>
    public ParameterSetQueryContext(
        IActorSupervisor supervisor,
        ILogger<ParameterSetQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, ParameterSetQueryActor.ActorName))
    {
        Logger = IsArgumentNull.Set(logger);
        signalSnapshots=new(()=>IsArgumentNull.Set(Container.Resolve<IRegimeDiscoveryMarketSignalSnapshotProvider>())!);
        startups = new(()=>IsArgumentNull.Set(Container.Resolve<IEventSourceActorStateRepository<ParameterStartupCommandState>>())!);
        accessPolicy = new(() => IsArgumentNull.Set(Container.Resolve<IParameterAccessPolicy>())!);
        assignments = new(() => IsArgumentNull.Set(Container.Resolve<IEventSourceActorStateRepository<ParameterAssignmentCommandState>>())!);
        repository = new(() => IsArgumentNull.Set(Container.Resolve<IEventSourceActorStateRepository<ParameterSetCommandState>>())!);
        configurationDb = new(() => IsArgumentNull.Set(Container.Resolve<IConfigurationDbContext>())!);
    }

    /// <inheritdoc />
    public IConfigurationDbContext ConfigurationDb => configurationDb.Value;
    public IEventSourceActorStateRepository<ParameterSetCommandState> StateRepository => repository.Value;
    public IEventSourceActorStateRepository<ParameterAssignmentCommandState> Assignments => assignments.Value;
    /// <inheritdoc />
    public ILogger<ParameterSetQueryActor> Logger { get; }
}
