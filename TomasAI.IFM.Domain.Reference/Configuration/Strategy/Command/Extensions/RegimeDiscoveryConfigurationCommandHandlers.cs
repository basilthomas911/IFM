using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Events;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Extensions;

/// <summary>Implements Regime Discovery configuration lifecycle command transitions.</summary>
public static class RegimeDiscoveryConfigurationCommandHandlers
{
    /// <summary>Creates one immutable Draft version.</summary>
    public static Task<ServiceResult<GuidResult>> ExecuteAsync(
        this CreateRegimeDiscoveryParameterSetCommand command,
        ICommandActorContext context,
        RegimeDiscoveryConfigurationCommandState state)
    {
        if (state.ParameterSet is not null)
            return Task.FromResult<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new(command.CommandId)));
        var created = new RegimeDiscoveryParameterSetCreatedEvent
        {
            Subject = EventSubject(RegimeDiscoveryParameterSetCreatedEvent.Verb, command.EntityId),
            EntityId = command.EntityId,
            ParameterSet = command.ParameterSet,
            Description = command.Description,
            CreatedBy = command.CreatedBy
        };
        return Result(state.Update(created, command), command.CommandId, "Draft creation was rejected.");
    }

    /// <summary>Publishes one Draft version.</summary>
    public static Task<ServiceResult<GuidResult>> ExecuteAsync(
        this PublishRegimeDiscoveryParameterSetCommand command,
        ICommandActorContext context,
        RegimeDiscoveryConfigurationCommandState state)
    {
        if (state.Status == "Published")
            return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
        var published = new RegimeDiscoveryParameterSetPublishedEvent
        {
            Subject = EventSubject(RegimeDiscoveryParameterSetPublishedEvent.Verb, command.EntityId),
            EntityId = command.EntityId,
            EffectiveFromUtc = command.EffectiveFromUtc
        };
        return Result(state.Update(published, command), command.CommandId, "Only a Draft version can be published.");
    }

    /// <summary>Retires one Published version.</summary>
    public static Task<ServiceResult<GuidResult>> ExecuteAsync(
        this RetireRegimeDiscoveryParameterSetCommand command,
        ICommandActorContext context,
        RegimeDiscoveryConfigurationCommandState state)
    {
        if (state.Status == "Retired")
            return Task.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
        var retired = new RegimeDiscoveryParameterSetRetiredEvent
        {
            Subject = EventSubject(RegimeDiscoveryParameterSetRetiredEvent.Verb, command.EntityId),
            EntityId = command.EntityId,
            RetiredAtUtc = command.RetiredAtUtc
        };
        return Result(state.Update(retired, command), command.CommandId, "Only a Published version can be retired.");
    }

    static Task<ServiceResult<GuidResult>> Result(bool applied, Guid commandId, string failure)
        => Task.FromResult<ServiceResult<GuidResult>>(applied
            ? new ServiceOk<GuidResult>(new(commandId))
            : new ServiceFailed<GuidResult>(33010, failure, new(commandId)));

    static ActorSubject EventSubject(string verb, RegimeDiscoveryParameterSetEntityId entityId)
        => new(ActorType.Event, RegimeDiscoveryParameterSetCreatedEvent.Actor, verb, entityId.Format());
}
