using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

static class ActorCommandResult
{
    internal static ServiceResult<Guid> ToGuidResult<TCommand, TEntityId>(
        TCommand command,
        ServiceResult<GuidResult> actorResult)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        => new()
        {
            Success = actorResult.Success,
            ErrorCode = actorResult.ErrorCode,
            ErrorMessage = actorResult.ErrorMessage,
            ErrorEvent = actorResult.ErrorEvent,
            Value = actorResult.Value?.Guid ?? command.CommandId
        };
}

