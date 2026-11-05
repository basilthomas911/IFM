using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.Persistence;

/// <summary>Preserves the atomic Portfolio and Policy source commit under the financial authority fence.</summary>
internal static class PortfolioPolicyAssignmentPersistence
{
    extension(IPortfolioEventStore events)
    {
        internal async Task<PortfolioFinancialPolicyAssignedEvent> CommitPolicyAssignmentAsync(
            ActivateAndAssignPortfolioFinancialPolicyCommand command, PortfolioFinancialPolicyReadModel policy,
            DateTime now, string principal, PortfolioFinancialPolicyActivatedEvent policyEvent,
            PortfolioFinancialPolicyAggregate policyState, CancellationToken token)
        {
            var id = new PortfolioId(command.EntityId.PortfolioId);
            var portfolio = await events.LoadPortfolioAsync(id, token).ConfigureAwait(false);
            if (portfolio.Revision != command.ExpectedPortfolioRevision)
                throw new InvalidOperationException($"Expected Portfolio revision {command.ExpectedPortfolioRevision}, actual {portfolio.Revision}.");
            if (command.ParentPortfolio is not { } prepared || portfolio.Current is not { } current
                || !string.Equals(PortfolioCanonicalHash.Compute(prepared), PortfolioCanonicalHash.Compute(current), StringComparison.Ordinal))
                throw new InvalidOperationException("Prepared Portfolio disagrees with its committed revision.");
            var assignment = CreateAssignment(command, portfolio, policy, now, principal);
            await events.AppendPolicyAndPortfolioWithStateAsync(command.EntityId, policyEvent, policyState,
                id, assignment, portfolio, token).ConfigureAwait(false);
            return assignment;
        }

        internal async Task<PortfolioFinancialPolicyAssignedEvent?> RepairPolicyAssignmentAsync(
            ActivateAndAssignPortfolioFinancialPolicyCommand command, PortfolioFinancialPolicyReadModel policy,
            DateTime now, string principal, CancellationToken token)
        {
            var id = new PortfolioId(command.EntityId.PortfolioId);
            var portfolio = await events.LoadPortfolioAsync(id, token).ConfigureAwait(false);
            if (portfolio.Current?.ActivePolicyId == policy.PolicyId && portfolio.Current.ActivePolicyVersion == policy.PolicyVersion)
                return null;
            var assignment = CreateAssignment(command, portfolio, policy, now, principal);
            await events.AppendPortfolioWithStateAsync(id, assignment, assignment.Revision - 1, portfolio,
                cancellationToken: token).ConfigureAwait(false);
            return assignment;
        }
    }

    static PortfolioFinancialPolicyAssignedEvent CreateAssignment(ActivateAndAssignPortfolioFinancialPolicyCommand command,
        PortfolioAggregate portfolio, PortfolioFinancialPolicyReadModel policy, DateTime now, string principal)
    {
        if (!command.ComputeAssignment(portfolio, policy, now, principal, out var change))
            throw new InvalidOperationException(change.RejectionReason);
        var assignment = command.CreatePortfolioFinancialPolicyAssignedEvent(change);
        if (!portfolio.Update(assignment, command))
            throw new InvalidOperationException("Portfolio policy assignment event could not be applied.");
        PortfolioAcceptedProjection.Capture(assignment, portfolio);
        return assignment;
    }
}
