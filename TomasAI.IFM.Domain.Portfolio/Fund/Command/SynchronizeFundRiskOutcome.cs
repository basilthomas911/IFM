using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the SynchronizeFundRiskOutcome command.</summary>
public static class SynchronizeFundRiskOutcome
{
    /// <summary>Applies the SynchronizeFundRiskOutcome transition to the loaded Fund aggregate.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this SynchronizeFundRiskOutcomeCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(
            aggregate.SynchronizeRisk(
            command.CommandId, command.ExpectedVersion, command.Evidence, now, principal));
}
