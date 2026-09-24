using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the CancelFundOrderComposition command.</summary>
public static class CancelFundOrderComposition
{
    /// <summary>Applies the CancelFundOrderComposition transition to the loaded Fund aggregate.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this CancelFundOrderCompositionCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(
            aggregate.CancelComposition(
            command.CommandId, aggregate.Revision, command.OrderId.OrderId,
            command.ExpectedVersion, command.Reason, now, principal));
}
