using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;

/// <summary>Defines readonly dependencies owned by the strategy-configuration Command actor.</summary>
public interface IParameterAssignmentCommandContext
    : ICommandActorContext<ParameterAssignmentCommandActor>
{
    IParameterAccessPolicy AccessPolicy { get; }
    /// <summary>Gets EventSourceDb.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets ConfigurationDb.</summary>
    IConfigurationDbContext ConfigurationDb { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<ParameterAssignmentCommandActor> EventProjector { get; }
    /// <summary>Gets the state repository.</summary>
    IEventSourceActorStateRepository<ParameterAssignmentCommandState> StateRepository { get; }
    /// <summary>Gets the projector queue dependency.</summary>
    IDurableReplayQueue DurableReplayQueue { get; }
    /// <summary>Gets the blackboard dependency.</summary>
    IBlackboardService BlackboardService { get; }
    /// <summary>Gets the state factory.</summary>
    IEventSourceActorStateFactory StateFactory { get; }
    /// <summary>Gets actor infrastructure.</summary>
    IActorService ActorService { get; }
    IEventSourceActorStateRepository<ParameterSetCommandState> ParameterSets { get; }
    /// <summary>Gets the logger.</summary>
    ILogger<ParameterAssignmentCommandActor> Logger { get; }
}
