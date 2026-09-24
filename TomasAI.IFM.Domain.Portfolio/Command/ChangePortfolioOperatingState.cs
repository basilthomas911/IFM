using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Handles the mapped ChangePortfolioOperatingState portfolio command.</summary>
public static class ChangePortfolioOperatingState
{
    /// <summary>Creates the domain event for the validated portfolio transition.</summary>
    public static IPortfolioDomainEvent Execute(this ChangePortfolioOperatingStateCommand command, PortfolioAggregate aggregate, DateTime now, string principal) =>
        aggregate.ChangeState(command.CommandId, command.ExpectedVersion, command.State, command.Reason, now, principal);
}
