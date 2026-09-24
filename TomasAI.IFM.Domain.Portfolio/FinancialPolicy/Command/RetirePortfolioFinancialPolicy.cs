using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command;

/// <summary>Handles the mapped RetirePortfolioFinancialPolicy command and its domain-event decision.</summary>
public static class RetirePortfolioFinancialPolicy
{
    /// <summary>Creates the policy event and delegates durable commit to the actor-owned persistence boundary.</summary>
    public static ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this RetirePortfolioFinancialPolicyCommand command,
        PortfolioFinancialPolicyAggregate aggregate,
        string principal,
        Func<Func<bool, DateTime, IPortfolioFinancialPolicyDomainEvent>,
            Func<IPortfolioFinancialPolicyDomainEvent, bool>?,
            ValueTask<ServiceResult<GuidResult>>> commit) =>
        commit((referenced, now) => aggregate.Retire(command.CommandId, command.ExpectedRevision, command.PolicyVersion, command.Reason, referenced, now, principal), null);
}
