using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command;

/// <summary>Handles the mapped RetirePortfolioFinancialPolicy command and its domain-event decision.</summary>
public static class RetirePortfolioFinancialPolicy
{
    /// <summary>Creates the policy event and delegates durable commit to the actor-owned persistence boundary.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="commit">The actor-owned durable commit boundary; invoked only after command replay checks.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    public static ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this RetirePortfolioFinancialPolicyCommand command,
        PortfolioFinancialPolicyAggregate state,
        string principal,
        Func<Func<bool, DateTime, ServiceResult<GuidResult>>,
            Func<IPortfolioFinancialPolicyDomainEvent, bool>?,
            ValueTask<ServiceResult<GuidResult>>> commit) =>
        commit((referenced, now) => command.Execute(state, principal, referenced, now), null);

    /// <summary>Computes and guards the policy change before applying its source event.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="referenced">Whether the current Portfolio references the policy.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    internal static ServiceResult<GuidResult> Execute(this RetirePortfolioFinancialPolicyCommand command, PortfolioFinancialPolicyAggregate state,
        string principal, bool referenced, DateTime now)
    {
        var errorMsg = $"{command.CommandName}: unable to apply PortfolioFinancialPolicyRetired event";
        var updated = command.Compute(state, principal, referenced, now, out var financialPolicyChange) switch
        {
            _ when !financialPolicyChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{financialPolicyChange.RejectionCode};{financialPolicyChange.RejectionReason}"),
            _ when financialPolicyChange.CommandId != command.CommandId
                => command.UpdateFailed(ref errorMsg, "Computed policy command identity does not match"),
            _ when financialPolicyChange.Revision != state.Revision + 1
                => command.UpdateFailed(ref errorMsg, "Computed policy revision is not the next revision"),
            _ => state.Update(command.CreatePortfolioFinancialPolicyRetiredEvent(financialPolicyChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable policy values without changing authoritative state.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="state">The owning authoritative command state.</param>
    /// <param name="principal">The authenticated and authorized operator.</param>
    /// <param name="referenced">Whether the current Portfolio references the policy.</param>
    /// <param name="now">The authoritative UTC decision time.</param>
    /// <param name="financialPolicyChange">The immutable computed financial-policy change.</param>
    /// <returns>True for an accepted calculation; false with the original business rejection reason otherwise.</returns>
    internal static bool Compute(this RetirePortfolioFinancialPolicyCommand command, PortfolioFinancialPolicyAggregate state,
        string principal, bool referenced, DateTime now, out PortfolioFinancialPolicyRetiredCompute financialPolicyChange)
    {
        try
        {
            financialPolicyChange = (PortfolioFinancialPolicyRetiredCompute)state.ComputeRetire(command.CommandId, command.ExpectedRevision, command.PolicyVersion, command.Reason, referenced, now, principal);
            return true;
        }
        catch (Exception rejection) when (rejection is ArgumentException or InvalidOperationException)
        {
            financialPolicyChange = new PortfolioFinancialPolicyRetiredCompute { Accepted = false, RejectionCode = "PortfolioFinancialPolicy.TransitionRejected", RejectionReason = rejection.Message };
            return false;
        }
    }

    /// <summary>Creates the accepted policy event with its originating command identity.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="financialPolicyChange">The immutable computed financial-policy change.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    internal static PortfolioFinancialPolicyRetiredEvent CreatePortfolioFinancialPolicyRetiredEvent(this RetirePortfolioFinancialPolicyCommand command, PortfolioFinancialPolicyRetiredCompute financialPolicyChange) => new()
    {
        Id = financialPolicyChange.Id,
        CommandId = command.CommandId,
        ReceivedOn = financialPolicyChange.OccurredOnUtc,
        Revision = financialPolicyChange.Revision,
        OccurredOnUtc = financialPolicyChange.OccurredOnUtc,
        Principal = financialPolicyChange.Principal,
        OriginatedOnUtc = financialPolicyChange.OccurredOnUtc,
        PolicyVersion = financialPolicyChange.PolicyVersion,
        Reason = financialPolicyChange.Reason,
    };
}
