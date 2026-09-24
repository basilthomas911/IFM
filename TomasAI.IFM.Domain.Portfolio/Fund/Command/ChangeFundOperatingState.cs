using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles a Fund operating-state transition.</summary>
public static class ChangeFundOperatingState
{
    /// <summary>Qualifies activation when required and applies the operating-state transition.</summary>
    public static async ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this ChangeFundOperatingStateCommand command,
        PortfolioFundId id,
        PortfolioFundAggregate aggregate,
        IPortfolioEventStore events,
        IReferenceQueryApi? referenceQueries,
        DateTime now,
        string principal,
        CancellationToken cancellationToken)
        => aggregate.ChangeState(command.CommandId, command.ExpectedVersion, command.State,
            command.Reason,
            await FundActivationQualification.EvaluateAsync(id, aggregate, events, referenceQueries,
                command.State == FundOperatingState.Active, cancellationToken).ConfigureAwait(false), now, principal);
}
