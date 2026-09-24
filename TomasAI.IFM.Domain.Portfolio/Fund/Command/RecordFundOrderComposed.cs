using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the RecordFundOrderComposed command.</summary>
public static class RecordFundOrderComposed
{
    /// <summary>Applies the RecordFundOrderComposed transition to the loaded Fund aggregate.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this RecordFundOrderComposedCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(
            aggregate.RecordCompositionResult(
            command.CommandId, aggregate.Revision, command.OrderId.OrderId,
            command.ExpectedVersion, command.Result, now, principal));
}
