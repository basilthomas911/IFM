using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the DeleteManualFundOrder command.</summary>
public static class DeleteManualFundOrder
{
    /// <summary>Deletes a draft manual Fund order.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this DeleteManualFundOrderCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(aggregate.DeleteManualOrder(
            command.CommandId, command.Request, now, principal));
}
