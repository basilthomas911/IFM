using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.State;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;

/// <summary>Read services for startup queries and legacy route compatibility.</summary>
public interface IParameterStartupQueryServices : IQueryActorContext
{
    IParameterAccessPolicy AccessPolicy { get; }
    IEventSourceActorStateRepository<ParameterAssignmentCommandState> Assignments { get; }
    IEventSourceActorStateRepository<ParameterStartupCommandState> Startups { get; }
    IEventSourceActorStateRepository<ParameterSetCommandState> StateRepository { get; }
    IConfigurationDbContext ConfigurationDb { get; }
    IRegimeDiscoveryMarketSignalSnapshotProvider SignalSnapshots { get; }
}
