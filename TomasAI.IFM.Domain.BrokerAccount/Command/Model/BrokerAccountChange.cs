using TomasAI.IFM.Domain.BrokerAccount.Contracts;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.Model;

/// <summary>Describes an immutable accepted account change, successful no-op, or rejection.</summary>
internal sealed record BrokerAccountChange(bool Accepted, BrokerAccountDefinition? BrokerAccountDefinition,
    bool IsUnchanged, string RejectionReason)
{
    /// <summary>Checks that accepted business data belongs to the requested account.</summary>
    internal bool IsValidFor(BrokerAccountId accountId) => accountId.IsValid && BrokerAccountDefinition?.Id == accountId;
    /// <summary>Accepts a proposed account definition.</summary>
    internal static BrokerAccountChange Accept(BrokerAccountDefinition brokerAccountDefinition) => new(true, brokerAccountDefinition, false, string.Empty);
    /// <summary>Acknowledges a duplicate without creating an event or changing its revision.</summary>
    internal static BrokerAccountChange Unchanged(BrokerAccountDefinition brokerAccountDefinition) => new(true, brokerAccountDefinition, true, string.Empty);
    /// <summary>Rejects the requested change without carrying account data.</summary>
    internal static BrokerAccountChange Reject(string rejectionReason) => new(false, null, false, rejectionReason);
}
