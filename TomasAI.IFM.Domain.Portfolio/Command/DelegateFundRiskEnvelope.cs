using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;

namespace TomasAI.IFM.Domain.Portfolio.Command;

/// <summary>Handles the mapped DelegateFundRiskEnvelope portfolio command.</summary>
public static class DelegateFundRiskEnvelope
{
    /// <summary>Creates the domain event for the validated portfolio transition.</summary>
    public static IPortfolioDomainEvent Execute(this DelegateFundRiskEnvelopeCommand command, PortfolioAggregate aggregate, DateTime now, string principal) =>
        aggregate.DelegateRiskEnvelope(command.CommandId, command.ExpectedPortfolioVersion, command.Envelope, now, principal);
}
