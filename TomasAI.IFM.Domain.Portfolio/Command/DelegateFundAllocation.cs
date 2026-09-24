using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Handles the mapped DelegateFundAllocation portfolio command.</summary>
public static class DelegateFundAllocation
{
    /// <summary>Creates the domain event for the validated portfolio transition.</summary>
    public static IPortfolioDomainEvent Execute(this DelegateFundAllocationCommand command, PortfolioAggregate aggregate, DateTime now, string principal) =>
        aggregate.DelegateAllocation(command.CommandId, command.ExpectedPortfolioVersion, command.Allocation, now, principal);
}
