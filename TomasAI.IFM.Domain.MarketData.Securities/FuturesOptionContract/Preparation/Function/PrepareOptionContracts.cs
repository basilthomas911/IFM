using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Preparation.Function;

internal static class PrepareOptionContracts
{
    extension(PrepareOptionContractsCommand request)
    {
        internal List<ValidationError> Validate()
        {
            if (request.CommandId == Guid.Empty ||
                request.EntityId != new ActorEntityId(request.CommandId.ToString("N")) ||
                request.Subject != new ActorSubject(ActorType.Function, PrepareOptionContractsCommand.Actor,
                    PrepareOptionContractsCommand.Verb, request.EntityId.Format()))
                return [new ValidationError("Invalid option preparation identity or route.")];
            if (request.Contracts is not { Length: > 0 } || request.Contracts.Any(static contract => contract is null))
                return [new ValidationError("Option preparation requires a nonempty batch of contracts.")];
            if (request.OriginalContractId is not null && request.Contracts.Length != 1)
                return [new ValidationError("Option change preparation requires a single contract.")];
            return [];
        }

        internal string GetFingerprint() =>
            Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                new { request.OriginalContractId, request.Contracts })));

        internal async ValueTask<OptionContractPreparationCompletedEvent> ExecuteAsync(IActorService actors, CancellationToken token)
        {
            FuturesOptionContractReadModel[] contracts = request.OriginalContractId is { } original
                ? [await FuturesOptionContractDbContext.EnrichFuturesOptionContractAsync(
                    request.Contracts[0], original, actors, token).ConfigureAwait(false)]
                : await FuturesOptionContractDbContext.EnrichContractsAsync(request.Contracts, actors, token).ConfigureAwait(false);
            return new()
            {
                Subject = new(ActorType.Event, "OptionContractPreparationEvent", "Completed", request.EntityId.Format()),
                Id = request.CommandId,
                CommandId = request.CommandId,
                EntityId = request.EntityId,
                EventSource = request.EventSource,
                ReceivedOn = DateTime.UtcNow,
                Contracts = contracts,
                OriginalContractId = request.OriginalContractId,
                RequestFingerprint = request.GetFingerprint()
            };
        }
    }

    extension(FunctionEventContext<PrepareOptionContractsCommand> input)
    {
        internal OptionContractPreparationFailedEvent CreateFailure(TimeProvider clock) => new()
        {
            Subject = new(ActorType.Event, "OptionContractPreparationEvent", "Failed", input.Request?.EntityId.Format() ?? "unknown"),
            EntityId = input.Request?.EntityId ?? ActorEntityId.Default,
            CommandId = input.Request?.CommandId ?? Guid.Empty,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            ErrorCode = PrepareOptionContractsCommand.ErrorId,
            ErrorType = input.IsConflict ? ErrorType.Concurrency : ErrorType.Command,
            ErrorMessage = input.IsConflict ? "Option preparation operation was reused with different inputs."
                : input.Exception?.Message ?? "Option preparation failed."
        };
    }
}
