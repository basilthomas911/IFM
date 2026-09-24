using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles the CreateFundMandate command.</summary>
public static class CreateFundMandate
{
    /// <summary>Applies the CreateFundMandate transition to the loaded Fund aggregate.</summary>
    public static ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this CreateFundMandateCommand command,
        PortfolioFundAggregate aggregate,
        DateTime now,
        string principal)
        => ValueTask.FromResult<IPortfolioFundDomainEvent?>(
            ((FundMandateCreatedEvent)aggregate.Create(
            command.CommandId, command.Mandate, now, principal)) with
            { IdempotencyKey = command.IdempotencyKey });
}
