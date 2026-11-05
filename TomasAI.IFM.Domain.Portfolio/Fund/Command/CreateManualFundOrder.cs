using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Portfolio.Command.State;
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

    /// <param name="command">
    /// The concrete business command and originating identity.
    /// </param>
    extension(CreateManualFundOrderCommand command)
    {
        /// <summary>Computes and guards the business change, then applies one source event.</summary>
        ///
        /// <param name="state">The owning authoritative command state.</param>
        /// <param name="now">The authoritative UTC decision time.</param>
        /// <param name="principal">The authenticated and authorized operator.</param>
        /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
        public ServiceResult<GuidResult> Execute(PortfolioFundAggregate state, DateTime now, string principal)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);

            if (state.TryComposition(command.Request.IdempotencyKey, out var prior))
            {
                var hash = PortfolioCanonicalHash.Compute(command.Request);
                if (!string.Equals(prior.CanonicalRequestSha256, hash, StringComparison.Ordinal))
                    throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different manual draft.");
                return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
            }

            var preparation = command.Preparation
                ?? throw new InvalidOperationException("Manual Fund order creation requires prepared identity and Portfolio evidence.");
            var parent = preparation.Parent;
            if (parent is null || parent.PortfolioId != command.Request.PortfolioId ||
                parent.PortfolioVersion != command.Request.PortfolioVersion || parent.OperatingState != PortfolioOperatingState.Active ||
                preparation.ParentSourceRevision is not > 0)
                throw new InvalidOperationException("Manual draft Portfolio version is stale or the Portfolio is not active.");
            var orderId = preparation.OrderId;
            var errorMsg = $"{command.CommandName}: unable to apply FundCompositionReserved event";
            var updated = command.Compute(state, now, principal, orderId, out var fundChange) switch
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
        ///
        /// <param name="state">The owning authoritative command state.</param>
        /// <param name="now">The authoritative UTC decision time.</param>
        /// <param name="principal">The authenticated and authorized operator.</param>
        /// <param name="orderId">The order id business input.</param>
        /// <param name="fundChange">The immutable computed Fund business change.</param>
        /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
        internal bool Compute(PortfolioFundAggregate state, DateTime now, string principal, int orderId, out FundCompositionReservedCompute fundChange)
        {
            try
            {
                fundChange = (FundCompositionReservedCompute)state.ComputeCreateManualOrder(command.CommandId, command.Request, orderId, now, principal);
                return true;
            }
            catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
            {
                fundChange = new FundCompositionReservedCompute { Accepted = false, RejectionCode = "PortfolioFund.TransitionRejected", RejectionReason = rejection.Message };
                return false;
            }
        }

        /// <summary>Creates the source event from accepted business values and preserves the command identity.</summary>
        ///
        /// <param name="fundChange">The immutable computed Fund business change.</param>
        /// <returns>The computed business values or created event; no transport or storage effects.</returns>
        internal FundCompositionReservedEvent CreateFundCompositionReservedEvent(FundCompositionReservedCompute fundChange) => new()
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
}
