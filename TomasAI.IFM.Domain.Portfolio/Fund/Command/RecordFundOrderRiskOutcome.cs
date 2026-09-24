using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the RecordFundOrderRiskOutcome command.</summary>
public static class RecordFundOrderRiskOutcome
{
    /// <summary>Applies the RecordFundOrderRiskOutcome transition to the loaded Fund aggregate.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this RecordFundOrderRiskOutcomeCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(
            aggregate.RecordRiskResult(
            command.CommandId, aggregate.Revision, command.OrderId.OrderId,
            command.ExpectedVersion, command.Result, now, principal));
}
