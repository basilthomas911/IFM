using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Handles deletion of a draft portfolio after checking child-fund composition history.</summary>
public static class DeleteDraftPortfolio
{
    /// <summary>Computes and guards the business change, then applies one source event.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="portfolioId">The parent Portfolio identity.</param>
    /// <param name="events">The authoritative event store used for lookup and commit.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this DeleteDraftPortfolioCommand command,
        PortfolioAggregate state,
        PortfolioId portfolioId,
        IPortfolioEventStore events,
        DateTime now,
        string principal,
        CancellationToken cancellationToken)
    {

        foreach (var fundId in state.FundIds)
        {
            var fund = await events.LoadFundAsync(new PortfolioFundId(portfolioId.Id, fundId), cancellationToken).ConfigureAwait(false);
            if (fund.Orders.Count != 0)
                throw new InvalidOperationException("A Draft Portfolio with composition history cannot be deleted.");
        }
                var errorMsg = $"{command.CommandName}: unable to apply DraftPortfolioDeleted event";
        var updated = command.Compute(state, now, principal, out var portfolioChange) switch
        {
            _ when !portfolioChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{portfolioChange.RejectionCode};{portfolioChange.RejectionReason}"),
            _ when portfolioChange.CommandId != command.CommandId
                => command.UpdateFailed(ref errorMsg, "Computed command identity does not match the originating command"),
            _ when portfolioChange.Revision != state.Revision + 1
                => command.UpdateFailed(ref errorMsg, "Computed aggregate revision is not the next revision"),
            _ => state.Update(command.CreateDraftPortfolioDeletedEvent(portfolioChange), command)
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
    /// <param name="portfolioChange">The immutable computed Portfolio business change.</param>
    /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
    internal static bool Compute(this DeleteDraftPortfolioCommand command, PortfolioAggregate state, DateTime now, string principal, out DraftPortfolioDeletedCompute portfolioChange)
    {
        try
        {
            portfolioChange = (DraftPortfolioDeletedCompute)state.ComputeDeleteDraft(command.CommandId, command.ExpectedVersion, command.Reason, now, principal);
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
        {
            portfolioChange = new DraftPortfolioDeletedCompute { Accepted = false, RejectionCode = "Portfolio.TransitionRejected", RejectionReason = rejection.Message };
            return false;
        }
    }

    /// <summary>Creates the source event from accepted business values and preserves the command identity.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="portfolioChange">The immutable computed Portfolio business change.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    internal static DraftPortfolioDeletedEvent CreateDraftPortfolioDeletedEvent(this DeleteDraftPortfolioCommand command, DraftPortfolioDeletedCompute portfolioChange) => new()
    {
        Id = portfolioChange.Id,
        CommandId = command.CommandId,
        ReceivedOn = portfolioChange.OccurredOnUtc,
        Revision = portfolioChange.Revision,
        OccurredOnUtc = portfolioChange.OccurredOnUtc,
        Principal = portfolioChange.Principal,
        OriginatedOnUtc = portfolioChange.OccurredOnUtc,
        Reason = portfolioChange.Reason,
    };
}
