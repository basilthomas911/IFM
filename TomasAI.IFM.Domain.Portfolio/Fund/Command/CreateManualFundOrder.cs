using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles manual Fund order creation after validating its Portfolio authority.</summary>
public static class CreateManualFundOrder
{
    /// <summary>Returns an existing identical draft or allocates and creates a new manual order.</summary>
    public static async ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this CreateManualFundOrderCommand command,
        PortfolioFundAggregate aggregate,
        IPortfolioEventStore events,
        IPortfolioBusinessIdAllocator allocator,
        DateTime now,
        string principal,
        CancellationToken cancellationToken)
    {
        if (aggregate.TryComposition(command.Request.IdempotencyKey, out var prior))
        {
            var hash = PortfolioCanonicalHash.Compute(command.Request);
            if (!string.Equals(prior.CanonicalRequestSha256, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different manual draft.");
            return null;
        }

        var portfolio = await events.LoadPortfolioAsync(new PortfolioId(command.Request.PortfolioId), cancellationToken).ConfigureAwait(false);
        if (portfolio.Current is null || portfolio.Current.PortfolioVersion != command.Request.PortfolioVersion ||
            portfolio.Current.OperatingState != PortfolioOperatingState.Active)
            throw new InvalidOperationException("Manual draft Portfolio version is stale or the Portfolio is not active.");
        var orderId = await allocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
        return aggregate.CreateManualOrder(command.CommandId, command.Request, orderId, now, principal);
    }
}
