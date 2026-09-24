using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the AddManualFundOrderTrade command.</summary>
public static class AddManualFundOrderTrade
{
    /// <summary>Adds a manually selected trade to a Fund order.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this AddManualFundOrderTradeCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(aggregate.AddManualTrade(
            command.CommandId, command.Request, now, principal));
}
