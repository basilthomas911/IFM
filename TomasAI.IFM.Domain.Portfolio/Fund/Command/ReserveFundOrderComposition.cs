using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles reservation of a Fund order composition request.</summary>
public static class ReserveFundOrderComposition
{
    /// <summary>Returns an identical prior reservation or allocates identities for a new composition.</summary>
    public static async ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this ReserveFundOrderCompositionCommand command,
        PortfolioFundAggregate aggregate,
        IPortfolioBusinessIdAllocator allocator,
        DateTime now,
        string principal,
        CancellationToken cancellationToken)
    {
        if (aggregate.TryComposition(command.Request.IdempotencyKey, out var prior))
        {
            var hash = PortfolioCanonicalHash.Compute(command.Request.DefensiveCopy());
            if (!string.Equals(prior.CanonicalRequestSha256, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different canonical request.");
            return null;
        }

        var orderId = await allocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
        var tradeIds = new int[command.Request.TradeInstructions.Length];
        for (var i = 0; i < tradeIds.Length; i++)
            tradeIds[i] = await allocator.AllocateTradeIdAsync(cancellationToken).ConfigureAwait(false);
        return aggregate.ReserveComposition(command.CommandId, aggregate.Revision, command.Request, command.Snapshot, orderId, tradeIds, now, principal);
    }
}
