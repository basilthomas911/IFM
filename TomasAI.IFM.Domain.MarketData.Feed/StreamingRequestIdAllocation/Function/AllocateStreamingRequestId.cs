using TomasAI.IFM.Application.Identity;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.StreamingRequestIdAllocation.Function;

public static class AllocateStreamingRequestId
{
    extension(AllocateStreamingRequestIdCommand request)
    {
        public List<ValidationError> Validate() =>
            request.CommandId == Guid.Empty ||
            request.EntityId != new ActorEntityId(request.CommandId.ToString("N")) ||
            request.Subject != new ActorSubject(ActorType.Function, AllocateStreamingRequestIdCommand.Actor, AllocateStreamingRequestIdCommand.Verb, request.EntityId.Format())
                ? [new ValidationError("Invalid operation identity or route.")]
                : [];

        public async ValueTask<StreamingRequestIdAllocationCompletedEvent> ExecuteAsync(ISequenceIdApi sequences, CancellationToken token)
        {
            var value = checked((int)await sequences.GetSequenceIdAsync(
                SequenceName.StreamingRequest_RequestId, token).ConfigureAwait(false));
            if (value <= 0)
                throw new InvalidOperationException("The streaming request ID must be positive.");
            return new()
            {
                Subject = new(ActorType.Event, "StreamingRequestIdAllocationEvent", "Completed", request.EntityId.Format()),
                Id = request.CommandId,
                CommandId = request.CommandId,
                EntityId = request.EntityId,
                EventSource = request.EventSource,
                ReceivedOn = DateTime.UtcNow,
                Value = value
            };
        }
    }

    extension(FunctionEventContext<AllocateStreamingRequestIdCommand> input)
    {
        public StreamingRequestIdAllocationFailedEvent CreateFailure(TimeProvider clock) => new()
        {
            Subject = new(ActorType.Event, "StreamingRequestIdAllocationEvent", "Failed", input.Request?.EntityId.Format() ?? "unknown"),
            EntityId = input.Request?.EntityId ?? ActorEntityId.Default,
            CommandId = input.Request?.CommandId ?? Guid.Empty,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            ErrorCode = AllocateStreamingRequestIdCommand.ErrorId,
            ErrorType = input.IsConflict ? ErrorType.Concurrency : ErrorType.Command,
            ErrorMessage = input.IsConflict ? "Operation identity was reused with different input." : input.Exception?.Message ?? "Operation failed."
        };
    }
}
