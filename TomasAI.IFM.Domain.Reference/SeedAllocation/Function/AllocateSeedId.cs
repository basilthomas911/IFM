using TomasAI.IFM.Domain.Reference.Shared.SeedAllocation;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.SeedAllocation.Function;

public static class AllocateSeedId
{
    extension(AllocateSeedIdCommand request)
    {
        public List<ValidationError> Validate() =>
            request.CommandId == Guid.Empty ||
            !string.Equals(request.CommandId.ToString("N"), request.EntityId.Format(), StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.SeedType)
                ? [new ValidationError("Invalid seed allocation identity or seed type.")]
                : [];

        public async ValueTask<SeedAllocationCompletedEvent> ExecuteAsync(ISequenceIdGenerator sequences, CancellationToken token)
        {
            var sequence = SequenceNameExtensions.ParseSequenceName(request.SeedType);
            var value = checked((int)await sequences.GetSequenceIdAsync(sequence, token).ConfigureAwait(false));
            return new()
            {
                Subject = new(ActorType.Event, "SeedAllocationEvent", "Allocated", request.EntityId.Format()),
                Id = request.CommandId,
                CommandId = request.CommandId,
                EntityId = request.EntityId,
                EventSource = request.EventSource,

                SeedType = request.SeedType,
                Value = value
            };
        }
    }

    extension(FunctionEventContext<AllocateSeedIdCommand> input)
    {
        public SeedAllocationFailedEvent CreateFailure(TimeProvider clock) => new()
        {
            Subject = new(ActorType.Event, "SeedAllocationEvent", "AllocationFailed", input.Request?.EntityId.Format() ?? "unknown"),
            EntityId = input.Request?.EntityId ?? ActorEntityId.Default,
            CommandId = input.Request?.CommandId ?? Guid.Empty,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            ErrorCode = AllocateSeedIdCommand.ErrorId,
            ErrorType = input.IsConflict ? ErrorType.Concurrency : ErrorType.Command,
            ErrorMessage = input.IsConflict ? "Seed allocation operation was reused with different input." : input.Exception?.Message ?? "Seed allocation failed."
        };
    }
}
