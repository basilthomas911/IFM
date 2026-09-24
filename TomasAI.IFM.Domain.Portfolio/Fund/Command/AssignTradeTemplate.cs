using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the AssignTradeTemplate command.</summary>
public static class AssignTradeTemplate
{
    /// <summary>Applies the AssignTradeTemplate transition to the loaded Fund aggregate.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this AssignTradeTemplateCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(
            aggregate.AssignTradeTemplate(
            command.CommandId, command.ExpectedVersion,
            command.Assignment, now, principal));
}
