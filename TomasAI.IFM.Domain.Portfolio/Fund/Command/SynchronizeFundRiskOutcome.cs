using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the SynchronizeFundRiskOutcome command.</summary>
public static class SynchronizeFundRiskOutcome
{
    /// <summary>Computes and guards the business change, then applies one source event.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    public static ServiceResult<GuidResult> Execute(
        this SynchronizeFundRiskOutcomeCommand command,
        PortfolioFundAggregate state,
        DateTime now,
        string principal)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FundCompositionStateChanged event";
        var updated = command.Compute(state, now, principal, out var fundChange) switch
        {
            _ when !fundChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{fundChange.RejectionCode};{fundChange.RejectionReason}"),
            _ when fundChange.CommandId != command.CommandId
                => command.UpdateFailed(ref errorMsg, "Computed command identity does not match the originating command"),
            _ when fundChange.Revision != state.Revision + 1
                => command.UpdateFailed(ref errorMsg, "Computed aggregate revision is not the next revision"),
            _ => state.Update(command.CreateFundCompositionStateChangedEvent(fundChange), command)
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
    /// <param name="fundChange">The immutable computed Fund business change.</param>
    /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
    internal static bool Compute(this SynchronizeFundRiskOutcomeCommand command, PortfolioFundAggregate state, DateTime now, string principal, out FundCompositionStateChangedCompute fundChange)
    {
        try
        {
            fundChange = (FundCompositionStateChangedCompute)state.ComputeSynchronizeRisk(
            command.CommandId, command.ExpectedVersion, command.Evidence, now, principal);
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
        {
            fundChange = new FundCompositionStateChangedCompute { Accepted = false, RejectionCode = "PortfolioFund.TransitionRejected", RejectionReason = rejection.Message };
            return false;
        }
    }

    /// <summary>Creates the source event from accepted business values and preserves the command identity.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="fundChange">The immutable computed Fund business change.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    internal static FundCompositionStateChangedEvent CreateFundCompositionStateChangedEvent(this SynchronizeFundRiskOutcomeCommand command, FundCompositionStateChangedCompute fundChange) => new()
    {
        Id = fundChange.Id,
        CommandId = command.CommandId,
        ReceivedOn = fundChange.OccurredOnUtc,
        Revision = fundChange.Revision,
        OccurredOnUtc = fundChange.OccurredOnUtc,
        Principal = fundChange.Principal,
        OriginatedOnUtc = fundChange.OccurredOnUtc,
        Order = fundChange.FundOrder,
    };
}
