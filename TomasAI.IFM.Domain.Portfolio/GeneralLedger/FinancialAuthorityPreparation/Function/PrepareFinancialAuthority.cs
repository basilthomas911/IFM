using TomasAI.IFM.Application.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using AuthorityPreparer = TomasAI.IFM.Domain.Portfolio.GeneralLedger.Preparation.FinancialAuthorityPreparation;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.FinancialAuthorityPreparation.Function;

public static class PrepareFinancialAuthority
{
    extension(PrepareFinancialAuthorityCommand request)
    {
        public List<ValidationError> Validate() =>
            request.CommandId == Guid.Empty || request.RouteTo != BoundedContextName.PortfolioBoundedContext || request.PostEvents ||
            request.EntityId != new ActorEntityId(request.CommandId.ToString("N")) ||
            request.Subject != new ActorSubject(ActorType.Function, PrepareFinancialAuthorityCommand.Actor, PrepareFinancialAuthorityCommand.Verb, request.EntityId.Format())
            || request.Scope?.Access is null || request.Scope.PortfolioId <= 0
            || request.Parameters is null
            || MessagePackBinarySerializer.MeasureContent(request) > 1_048_576
                ? [new ValidationError("Invalid operation identity or route.")]
                : [];

        public async ValueTask<FinancialAuthorityPreparationCompletedEvent> ExecuteAsync(AuthorityPreparer preparation, CancellationToken token)
        {
            var value = await preparation.PrepareAsync(request.Scope, request.Parameters, token).ConfigureAwait(false);
            if (MessagePackBinarySerializer.MeasureContent(value) > 524_288)
                throw new FinancialOperationException(FinancialReasons.InvalidContract, "Financial preparation result exceeds 512 KiB.");
            return new()
            {
                Subject = new(ActorType.Event, "FinancialAuthorityPreparationEvent", "Completed", request.EntityId.Format()),
                Id = request.CommandId,
                CommandId = request.CommandId,
                EntityId = request.EntityId,
                EventSource = request.EventSource,
                ReceivedOn = DateTime.UtcNow,
                InputHash = FinancialCanonicalHash.Compute(new { request.Parameters, request.Scope }),
                Value = value
            };
        }
    }

    extension(FunctionEventContext<PrepareFinancialAuthorityCommand> input)
    {
        public FinancialAuthorityPreparationFailedEvent CreateFailure(TimeProvider clock) => new()
        {
            Subject = new(ActorType.Event, "FinancialAuthorityPreparationEvent", "Failed", input.Request?.EntityId.Format() ?? "unknown"),
            EntityId = input.Request?.EntityId ?? ActorEntityId.Default,
            CommandId = input.Request?.CommandId ?? Guid.Empty,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            ErrorCode = input.Exception switch
            {
                FinancialOperationException error => error.Code,
                PortfolioAuthorizationException => PortfolioErrorCodes.Unauthorized,
                PortfolioOperationalException => PortfolioErrorCodes.OperationallyDisabled,
                _ => PrepareFinancialAuthorityCommand.ErrorId
            },
            ErrorType = input.IsConflict ? ErrorType.Concurrency : ErrorType.Command,
            ErrorMessage = input.IsConflict ? "Operation identity was reused with different input." : input.Exception?.Message ?? "Operation failed."
        };
    }
}
