using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the CloseManualFundOrder command.</summary>
public static class CloseManualFundOrder
{
    /// <summary>Closes a manual Fund order.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this CloseManualFundOrderCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(aggregate.CloseManualOrder(
            command.CommandId, command.Request, now, principal));
}
