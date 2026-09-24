using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the RemoveManualFundOrderTrade command.</summary>
public static class RemoveManualFundOrderTrade
{
    /// <summary>Removes a manually selected trade from a Fund order.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this RemoveManualFundOrderTradeCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(aggregate.RemoveManualTrade(
            command.CommandId, command.Request, now, principal));
}
