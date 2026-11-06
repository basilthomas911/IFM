using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FeedSessionPreparation.Function;

public static class PrepareMarketDataFeedSession
{
    extension(PrepareMarketDataFeedSessionCommand request)
    {
        public List<ValidationError> Validate() =>
            request.CommandId == Guid.Empty ||
            request.EntityId != new ActorEntityId(request.CommandId.ToString("N")) ||
            request.Subject != new ActorSubject(ActorType.Function, PrepareMarketDataFeedSessionCommand.Actor, PrepareMarketDataFeedSessionCommand.Verb, request.EntityId.Format())
                ? [new ValidationError("Invalid operation identity or route.")]
                : [];

        public async ValueTask<FeedSessionPreparationCompletedEvent> ExecuteAsync(IValueDateProvider dates, IMarketDataApi marketData, CancellationToken token)
        {
            var valueDate = dates.ValueDate;
            if (valueDate == default)
                throw new InvalidOperationException("The API value-date provider has not been initialized.");
            var contracts = await marketData.PrepareSessionAsync(valueDate, token).ConfigureAwait(false);
            if (contracts is not { Length: > 0 })
                throw new InvalidOperationException("The API has no prepared market-data contract assignments.");
            return new()
            {
                Subject = new(ActorType.Event, "FeedSessionPreparationEvent", "Completed", request.EntityId.Format()),
                Id = request.CommandId,
                CommandId = request.CommandId,
                EntityId = request.EntityId,
                EventSource = request.EventSource,
                ReceivedOn = DateTime.UtcNow,
                ValueDate = valueDate,
                Contracts = contracts
            };
        }
    }

    extension(FunctionEventContext<PrepareMarketDataFeedSessionCommand> input)
    {
        public FeedSessionPreparationFailedEvent CreateFailure(TimeProvider clock) => new()
        {
            Subject = new(ActorType.Event, "FeedSessionPreparationEvent", "Failed", input.Request?.EntityId.Format() ?? "unknown"),
            EntityId = input.Request?.EntityId ?? ActorEntityId.Default,
            CommandId = input.Request?.CommandId ?? Guid.Empty,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            ErrorCode = PrepareMarketDataFeedSessionCommand.ErrorId,
            ErrorType = input.IsConflict ? ErrorType.Concurrency : ErrorType.Command,
            ErrorMessage = input.IsConflict ? "Operation identity was reused with different input." : input.Exception?.Message ?? "Operation failed."
        };
    }
}
