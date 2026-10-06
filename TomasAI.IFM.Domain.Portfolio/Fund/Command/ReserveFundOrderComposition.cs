using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles reservation of a Fund order composition request.</summary>
public static class ReserveFundOrderComposition
{
    /// <summary>Computes and guards the business change, then applies one source event.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="allocator">The durable business identity allocator.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this ReserveFundOrderCompositionCommand command,
        PortfolioFundAggregate state,
        IPortfolioBusinessIdAllocator allocator,
        DateTime now,
        string principal,
        CancellationToken cancellationToken)
    {

        if (state.TryComposition(command.Request.IdempotencyKey, out var prior))
        {
            var hash = PortfolioCanonicalHash.Compute(command.Request.DefensiveCopy());
            if (!string.Equals(prior.CanonicalRequestSha256, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different canonical request.");
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        }

        var orderId = await allocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
        var tradeIds = new int[command.Request.TradeInstructions.Length];
        for (var i = 0; i < tradeIds.Length; i++)
            tradeIds[i] = await allocator.AllocateTradeIdAsync(cancellationToken).ConfigureAwait(false);
                var errorMsg = $"{command.CommandName}: unable to apply FundCompositionReserved event";
        var updated = command.Compute(state, now, principal, orderId, tradeIds, out var fundChange) switch
        {
            _ when !fundChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{fundChange.RejectionCode};{fundChange.RejectionReason}"),
            _ when fundChange.CommandId != command.CommandId
                => command.UpdateFailed(ref errorMsg, "Computed command identity does not match the originating command"),
            _ when fundChange.Revision != state.Revision + 1
                => command.UpdateFailed(ref errorMsg, "Computed aggregate revision is not the next revision"),
            _ => state.Update(command.CreateFundCompositionReservedEvent(fundChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }
    /// <summary>Computes immutable business values without changing authoritative state.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="orderId">The order id business input.</param>
    /// <param name="tradeIds">The trade ids business input.</param>
    /// <param name="fundChange">The immutable computed Fund business change.</param>
    /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
    internal static bool Compute(this ReserveFundOrderCompositionCommand command, PortfolioFundAggregate state, DateTime now, string principal, int orderId, IReadOnlyList<int> tradeIds, out FundCompositionReservedCompute fundChange)
    {
        try
        {
            fundChange = (FundCompositionReservedCompute)state.ComputeReserveComposition(command.CommandId, state.Revision, command.Request, command.Snapshot, orderId, tradeIds, now, principal);
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
        {
            fundChange = new FundCompositionReservedCompute { Accepted = false, RejectionCode = "PortfolioFund.TransitionRejected", RejectionReason = rejection.Message };
            return false;
        }
    }

    /// <summary>Creates the source event from accepted business values and preserves the command identity.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="fundChange">The immutable computed Fund business change.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    internal static FundCompositionReservedEvent CreateFundCompositionReservedEvent(this ReserveFundOrderCompositionCommand command, FundCompositionReservedCompute fundChange) => new()
    {
        Id = fundChange.Id,
        CommandId = command.CommandId,
        ReceivedOn = fundChange.OccurredOnUtc,
        Revision = fundChange.Revision,
        OccurredOnUtc = fundChange.OccurredOnUtc,
        Principal = fundChange.Principal,
        OriginatedOnUtc = fundChange.OccurredOnUtc,
        Reservation = fundChange.FundCompositionReservation,
    };
}
