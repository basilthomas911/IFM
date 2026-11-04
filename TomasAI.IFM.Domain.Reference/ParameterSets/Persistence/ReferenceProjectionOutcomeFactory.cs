using TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;

internal static class ReferenceProjectionOutcomeFactory
{
    internal static TOutcome Complete<TEntityId, TOutcome>(IEvent<TEntityId> source, string actor)
        where TEntityId : IActorEntityId
        where TOutcome : ReferenceProjectionComplete<TEntityId>, new() => new()
    {
        Subject = Subject<TOutcome>(source, actor),
        Id = source.Id,
        EntityId = source.EntityId,
        EventId = source.EventId,
        CommandId = source.CommandId,
        AggregateId = source.AggregateId,
        EventSource = source.EventSource,
        ReceivedOn = source.ReceivedOn
    };

    internal static TOutcome Fail<TEntityId, TOutcome>(IEvent<TEntityId> source, string actor, Exception error)
        where TEntityId : IActorEntityId
        where TOutcome : ReferenceProjectionFail<TEntityId>, new() => new()
    {
        Subject = Subject<TOutcome>(source, actor),
        Id = source.Id,
        EntityId = source.EntityId,
        EventId = source.EventId,
        CommandId = source.CommandId,
        AggregateId = source.AggregateId,
        EventSource = source.EventSource,
        ReceivedOn = source.ReceivedOn,
        ErrorType = ErrorType.Denormalizer,
        ErrorCode = 33020,
        ErrorMessage = error.Message
    };

    static ActorSubject Subject<TOutcome>(IEvent source, string actor)
    {
        var name = typeof(TOutcome).Name;
        return new(ActorType.Event, actor, name[..^"Event".Length], source.Subject.EntityId);
    }
}
