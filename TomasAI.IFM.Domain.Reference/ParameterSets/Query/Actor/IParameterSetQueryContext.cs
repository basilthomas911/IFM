using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Query.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Query.Actor;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.State;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;

/// <summary>Set queries, with read-only compatibility for published legacy query routes.</summary>
public interface IParameterSetQueryContext : IQueryActorContext<ParameterSetQueryActor>, IParameterAssignmentQueryServices, IParameterStartupQueryServices
{
    new IParameterAccessPolicy AccessPolicy { get; }
    new IEventSourceActorStateRepository<ParameterAssignmentCommandState> Assignments { get; }
    ILogger<ParameterSetQueryActor> Logger { get; }
}
