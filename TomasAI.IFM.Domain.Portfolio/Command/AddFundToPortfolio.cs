using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Handles the mapped AddFundToPortfolio portfolio command.</summary>
public static class AddFundToPortfolio
{
    /// <summary>Creates the domain event for the validated portfolio transition.</summary>
    public static IPortfolioDomainEvent Execute(this AddFundToPortfolioCommand command, PortfolioAggregate aggregate, DateTime now, string principal) =>
        aggregate.AddFund(command.CommandId, command.ExpectedPortfolioVersion, command.FundId, now, principal);
}
