using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the ExpireFundOrderComposition command.</summary>
public static class ExpireFundOrderComposition
{
    /// <summary>Applies the ExpireFundOrderComposition transition to the loaded Fund aggregate.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this ExpireFundOrderCompositionCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(
            aggregate.ExpireComposition(
            command.CommandId, aggregate.Revision, command.OrderId.OrderId,
            command.ExpectedVersion, command.Reason, now, principal));
}
