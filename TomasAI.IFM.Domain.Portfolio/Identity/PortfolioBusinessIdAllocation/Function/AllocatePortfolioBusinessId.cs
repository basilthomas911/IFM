using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Identity.PortfolioBusinessIdAllocation.Function;

public static class AllocatePortfolioBusinessId
{
    extension(AllocatePortfolioBusinessIdCommand request)
    {
        public List<ValidationError> Validate() =>
            request.CommandId == Guid.Empty || request.RouteTo != BoundedContextName.PortfolioBoundedContext || request.PostEvents ||
            request.EntityId != new ActorEntityId(request.CommandId.ToString("N")) ||
            request.Subject != new ActorSubject(ActorType.Function, AllocatePortfolioBusinessIdCommand.Actor, AllocatePortfolioBusinessIdCommand.Verb, request.EntityId.Format())
            || request.Access is null
            || !Enum.IsDefined(request.Kind)
                ? [new ValidationError("Invalid operation identity or route.")]
                : [];

        public async ValueTask<PortfolioBusinessIdAllocationCompletedEvent> ExecuteAsync(IPortfolioBusinessIdAllocator identities, IPortfolioOperationalGuard guard, CancellationToken token)
        {
            var operation = request.Kind is (PortfolioBusinessIdentityKind.Order or PortfolioBusinessIdentityKind.Trade)
                && request.Access.Roles.Contains(PortfolioOperationalPolicy.WorkflowRole, StringComparer.OrdinalIgnoreCase)
                ? PortfolioOperation.ReserveComposition : PortfolioOperation.AdministerPortfolio;
            guard.Demand(operation, request.Access, mutation: true);
            var identity = request.Kind switch
            {
                PortfolioBusinessIdentityKind.Portfolio => (await identities.AllocatePortfolioIdAsync(token).ConfigureAwait(false)).Id,
                PortfolioBusinessIdentityKind.Fund => await identities.AllocateFundIdAsync(token).ConfigureAwait(false),
                PortfolioBusinessIdentityKind.Order => await identities.AllocateOrderIdAsync(token).ConfigureAwait(false),
                PortfolioBusinessIdentityKind.Trade => await identities.AllocateTradeIdAsync(token).ConfigureAwait(false),
                PortfolioBusinessIdentityKind.Policy => await identities.AllocatePolicyIdAsync(token).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(request))
            };
            if (identity <= 0)
                throw new InvalidOperationException("The allocated Portfolio identity must be positive.");
            var value = new Shared.ServiceApi.PortfolioBusinessIdAllocation
            {
                Kind = request.Kind, Value = identity, CorrelationId = request.CommandId, OperationId = request.CommandId
            };
            return new()
            {
                Subject = new(ActorType.Event, "PortfolioBusinessIdAllocationEvent", "Completed", request.EntityId.Format()),
                Id = request.CommandId,
                CommandId = request.CommandId,
                EntityId = request.EntityId,
                EventSource = request.EventSource,
                ReceivedOn = DateTime.UtcNow,
                InputHash = FinancialCanonicalHash.Compute(new { request.Kind, request.Access }),
                Value = value
            };
        }
    }

    extension(FunctionEventContext<AllocatePortfolioBusinessIdCommand> input)
    {
        public PortfolioBusinessIdAllocationFailedEvent CreateFailure(TimeProvider clock) => new()
        {
            Subject = new(ActorType.Event, "PortfolioBusinessIdAllocationEvent", "Failed", input.Request?.EntityId.Format() ?? "unknown"),
            EntityId = input.Request?.EntityId ?? ActorEntityId.Default,
            CommandId = input.Request?.CommandId ?? Guid.Empty,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            ErrorCode = input.Exception switch
            {
                FinancialOperationException error => error.Code,
                PortfolioAuthorizationException => PortfolioErrorCodes.Unauthorized,
                PortfolioOperationalException => PortfolioErrorCodes.OperationallyDisabled,
                _ => AllocatePortfolioBusinessIdCommand.ErrorId
            },
            ErrorType = input.IsConflict ? ErrorType.Concurrency : ErrorType.Command,
            ErrorMessage = input.IsConflict ? "Operation identity was reused with different input." : input.Exception?.Message ?? "Operation failed."
        };
    }
}
