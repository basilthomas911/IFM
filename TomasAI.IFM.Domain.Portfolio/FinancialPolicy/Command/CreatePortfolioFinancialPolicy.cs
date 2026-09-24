using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command;

/// <summary>Handles the mapped CreatePortfolioFinancialPolicy command and its domain-event decision.</summary>
public static class CreatePortfolioFinancialPolicy
{
    /// <summary>Creates the policy event and delegates durable commit to the actor-owned persistence boundary.</summary>
    public static ValueTask<ServiceResult<GuidResult>> ExecuteAsync(
        this CreatePortfolioFinancialPolicyCommand command,
        PortfolioFinancialPolicyAggregate aggregate,
        string principal,
        Func<Func<bool, DateTime, IPortfolioFinancialPolicyDomainEvent>,
            Func<IPortfolioFinancialPolicyDomainEvent, bool>?,
            ValueTask<ServiceResult<GuidResult>>> commit) =>
        commit((_, now) => aggregate.Create(command.CommandId, command.IdempotencyKey, command.Policy, now, principal), committed => committed is PortfolioFinancialPolicyCreatedEvent prior && !string.Equals(command.Policy.CanonicalSha256(), prior.Policy.CanonicalSha256(), StringComparison.Ordinal));
}
