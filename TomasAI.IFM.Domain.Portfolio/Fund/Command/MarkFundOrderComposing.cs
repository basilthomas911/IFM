using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the MarkFundOrderComposing command.</summary>
public static class MarkFundOrderComposing
{
    /// <summary>Applies the MarkFundOrderComposing transition to the loaded Fund aggregate.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this MarkFundOrderComposingCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(
            aggregate.MarkCompositionComposing(
            command.CommandId, aggregate.Revision, command.OrderId.OrderId,
            command.ExpectedVersion, now, principal));
}
