using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command;

/// <summary>Handles addition of a new Fund mandate version.</summary>
public static class AddFundMandateVersion
{
    /// <summary>Qualifies activation when required and applies the mandate-version transition.</summary>
    public static async ValueTask<IPortfolioFundDomainEvent?> ExecuteAsync(
        this AddFundMandateVersionCommand command,
        PortfolioFundId id,
        PortfolioFundAggregate aggregate,
        IPortfolioEventStore events,
        IReferenceQueryApi? referenceQueries,
        DateTime now,
        string principal,
        CancellationToken cancellationToken)
        => aggregate.AddVersion(command.CommandId, command.ExpectedVersion, command.Mandate,
            await FundActivationQualification.EvaluateAsync(id, aggregate, events, referenceQueries,
                command.Mandate.OperatingState == FundOperatingState.Active, cancellationToken).ConfigureAwait(false), now, principal);
}
