using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles a Fund operating-state transition.</summary>
public static class ChangeFundOperatingState
{
    /// <summary>Computes and guards the business change, then applies one source event.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="id">The id business input.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="events">The authoritative event store used for lookup and commit.</param>
    /// <param name="referenceQueries">The reference catalog used to qualify exact business selections.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this ChangeFundOperatingStateCommand command,
        PortfolioFundId id,
        PortfolioFundAggregate state,
        IPortfolioEventStore events,
        IReferenceQueryApi? referenceQueries,
        DateTime now,
        string principal,
        CancellationToken cancellationToken)
    {
        var activation = await FundActivationQualification.EvaluateAsync(id, state, events, referenceQueries,
                command.State == FundOperatingState.Active, cancellationToken).ConfigureAwait(false);
        var errorMsg = $"{command.CommandName}: unable to apply FundOperatingStateChanged event";
        var updated = command.Compute(state, now, principal, activation, out var fundChange) switch
        {
            _ when !fundChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{fundChange.RejectionCode};{fundChange.RejectionReason}"),
            _ when fundChange.CommandId != command.CommandId
                => command.UpdateFailed(ref errorMsg, "Computed command identity does not match the originating command"),
            _ when fundChange.Revision != state.Revision + 1
                => command.UpdateFailed(ref errorMsg, "Computed aggregate revision is not the next revision"),
            _ => state.Update(command.CreateFundOperatingStateChangedEvent(fundChange), command)
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
    /// <param name="activation">The qualified Fund activation evidence.</param>
    /// <param name="fundChange">The immutable computed Fund business change.</param>
    /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
    internal static bool Compute(this ChangeFundOperatingStateCommand command, PortfolioFundAggregate state, DateTime now, string principal, FundActivationContext activation, out FundOperatingStateChangedCompute fundChange)
    {
        try
        {
            fundChange = (FundOperatingStateChangedCompute)state.ComputeChangeState(command.CommandId, command.ExpectedVersion, command.State,
            command.Reason,
            activation, now, principal);
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
        {
            fundChange = new FundOperatingStateChangedCompute { Accepted = false, RejectionCode = "PortfolioFund.TransitionRejected", RejectionReason = rejection.Message };
            return false;
        }
    }

    /// <summary>Creates the source event from accepted business values and preserves the command identity.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="fundChange">The immutable computed Fund business change.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    internal static FundOperatingStateChangedEvent CreateFundOperatingStateChangedEvent(this ChangeFundOperatingStateCommand command, FundOperatingStateChangedCompute fundChange) => new()
    {
        Id = fundChange.Id,
        CommandId = command.CommandId,
        ReceivedOn = fundChange.OccurredOnUtc,
        Revision = fundChange.Revision,
        OccurredOnUtc = fundChange.OccurredOnUtc,
        Principal = fundChange.Principal,
        OriginatedOnUtc = fundChange.OccurredOnUtc,
        State = fundChange.OperatingState,
        Reason = fundChange.Reason,
    };
}
