using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Handles the mapped creation of a portfolio.</summary>
public static class CreatePortfolio
{
    /// <summary>Creates the initial portfolio domain event.</summary>
    public static PortfolioCreatedEvent Execute(this CreatePortfolioCommand command, PortfolioAggregate aggregate, DateTime now, string principal) =>
        ((PortfolioCreatedEvent)aggregate.Create(command.CommandId, command.Portfolio, now, principal)) with
        { IdempotencyKey = command.IdempotencyKey };
}
