using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Handles the mapped RetirePortfolio portfolio command.</summary>
public static class RetirePortfolio
{
    /// <summary>Creates the domain event for the validated portfolio transition.</summary>
    public static IPortfolioDomainEvent Execute(this RetirePortfolioCommand command, PortfolioAggregate aggregate, DateTime now, string principal) =>
        aggregate.Retire(command.CommandId, command.ExpectedVersion, command.Reason, now, principal);
}
