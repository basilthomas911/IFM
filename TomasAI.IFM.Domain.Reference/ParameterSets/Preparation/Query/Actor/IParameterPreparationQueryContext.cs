using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Actor;

/// <summary>Read-only owner services used to prepare explicit command evidence.</summary>
public interface IParameterPreparationQueryContext : IQueryActorContext<ParameterPreparationQueryActor>
{
    IParameterAccessPolicy AccessPolicy { get; }
    IConfigurationDbContext ConfigurationDb { get; }
    IEventSourceActorStateRepository<ParameterSetCommandState> ParameterSets { get; }
    IEventSourceActorStateRepository<ParameterAssignmentCommandState> Assignments { get; }
    IEventSourceActorStateRepository<ParameterStartupCommandState> Startups { get; }
    ILogger<ParameterPreparationQueryActor> Logger { get; }
}
