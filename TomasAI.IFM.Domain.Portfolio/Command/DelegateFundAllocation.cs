using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Handles the mapped DelegateFundAllocation portfolio command.</summary>
public static class DelegateFundAllocation
{
    /// <summary>Computes and guards the business change, then applies one source event.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    public static ServiceResult<GuidResult> Execute(this DelegateFundAllocationCommand command, PortfolioAggregate state, DateTime now, string principal)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FundAllocationDelegated event";
        var updated = command.Compute(state, now, principal, out var portfolioChange) switch
        {
            _ when !portfolioChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{portfolioChange.RejectionCode};{portfolioChange.RejectionReason}"),
            _ when portfolioChange.CommandId != command.CommandId
                => command.UpdateFailed(ref errorMsg, "Computed command identity does not match the originating command"),
            _ when portfolioChange.Revision != state.Revision + 1
                => command.UpdateFailed(ref errorMsg, "Computed aggregate revision is not the next revision"),
            _ => state.Update(command.CreateFundAllocationDelegatedEvent(portfolioChange), command)
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
    internal static bool Compute(this DelegateFundAllocationCommand command, PortfolioAggregate state, DateTime now, string principal, out FundAllocationDelegatedCompute portfolioChange)
    {
        try
        {
            portfolioChange = (FundAllocationDelegatedCompute)state.ComputeDelegateAllocation(command.CommandId, command.ExpectedPortfolioVersion, command.Allocation, now, principal);
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
        {
            portfolioChange = new FundAllocationDelegatedCompute { Accepted = false, RejectionCode = "Portfolio.TransitionRejected", RejectionReason = rejection.Message };
            return false;
        }
    }

    /// <summary>Creates the source event from accepted business values and preserves the command identity.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="portfolioChange">The immutable computed Portfolio business change.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    internal static FundAllocationDelegatedEvent CreateFundAllocationDelegatedEvent(this DelegateFundAllocationCommand command, FundAllocationDelegatedCompute portfolioChange) => new()
    {
        Id = portfolioChange.Id,
        CommandId = command.CommandId,
        ReceivedOn = portfolioChange.OccurredOnUtc,
        Revision = portfolioChange.Revision,
        OccurredOnUtc = portfolioChange.OccurredOnUtc,
        Principal = portfolioChange.Principal,
        OriginatedOnUtc = portfolioChange.OccurredOnUtc,
        Allocation = portfolioChange.FundAllocation,
    };
}
