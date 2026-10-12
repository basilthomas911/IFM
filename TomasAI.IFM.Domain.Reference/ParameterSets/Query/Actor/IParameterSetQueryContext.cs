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
    IRegimeDiscoveryMarketSignalSnapshotProvider SignalSnapshots { get; }
    IParameterAccessPolicy AccessPolicy { get; }
    IEventSourceActorStateRepository<ParameterStartupCommandState> Startups { get; }
    /// <summary>Gets ConfigurationDb.</summary>
    IConfigurationDbContext ConfigurationDb { get; }
    IEventSourceActorStateRepository<ParameterSetCommandState> StateRepository { get; }
    IEventSourceActorStateRepository<ParameterAssignmentCommandState> Assignments { get; }
    /// <summary>Gets the logger.</summary>
    ILogger<ParameterSetQueryActor> Logger { get; }
}
