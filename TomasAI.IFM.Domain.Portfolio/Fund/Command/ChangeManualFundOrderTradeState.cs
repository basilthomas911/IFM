using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the ChangeManualFundOrderTradeState command.</summary>
public static class ChangeManualFundOrderTradeState
{
    /// <summary>Changes the state of a manual Fund order trade.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this ChangeManualFundOrderTradeStateCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(aggregate.ChangeManualTradeState(
            command.CommandId, command.Request, now, principal));
}
