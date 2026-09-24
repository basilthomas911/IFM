using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Handles the mapped AddPortfolioVersion portfolio command.</summary>
public static class AddPortfolioVersion
{
    /// <summary>Creates the domain event for the validated portfolio transition.</summary>
    public static IPortfolioDomainEvent Execute(this AddPortfolioVersionCommand command, PortfolioAggregate aggregate, DateTime now, string principal) =>
        aggregate.AddVersion(command.CommandId, command.ExpectedVersion, command.Portfolio, now, principal);
}
