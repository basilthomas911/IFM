using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.Model;

/// <summary>Computes account qualification, evidence, holds, and snapshot changes without mutation.</summary>
internal static class BrokerAccountComputation
{
    /// <summary>Computes AcceptAccountQualification without changing account state.</summary>
    internal static BrokerAccountChange AcceptAccountQualification(AcceptAccountQualificationCommand command, BrokerAccountDefinition? currentAccount)
    {
        var brokerAccountDefinition = currentAccount;
        if (brokerAccountDefinition is null || brokerAccountDefinition.QualificationStatus != BrokerAccountQualificationStatus.ReviewPending)
            return BrokerAccountChange.Reject("BrokerAccount.QUALIFICATION.NOT_REVIEW_PENDING");
        if (!string.Equals(brokerAccountDefinition.ManifestHash, command.ManifestHash, StringComparison.Ordinal))
            return BrokerAccountChange.Reject("BrokerAccount.QUALIFICATION.MANIFEST_CONFLICT");
        var changedAccount = brokerAccountDefinition with
        {
            QualificationStatus = BrokerAccountQualificationStatus.Accepted,
            ApprovalId = command.ApprovalId,
            AuthorizedBy = command.AuthorizedBy,
            ChangedAtUtc = command.ReviewedAtUtc,
            Revision = brokerAccountDefinition.Revision + 1
        };
        changedAccount = changedAccount with { Gate = Gate(changedAccount) };
        return BrokerAccountChange.Accept(changedAccount);
    }

    /// <summary>Computes RecordBrokerAccountSnapshot without changing account state.</summary>
    internal static BrokerAccountChange RecordBrokerAccountSnapshot(RecordBrokerAccountSnapshotCommand command, BrokerAccountDefinition? currentAccount)
    {
        var brokerAccountDefinition = currentAccount ?? new BrokerAccountDefinition
        {
            Id = command.EntityId,
            Environment = command.Environment,
            QualificationStatus = BrokerAccountQualificationStatus.EvidencePending
        };
        if (brokerAccountDefinition.Environment != BrokerEnvironment.Unknown && brokerAccountDefinition.Environment != command.Environment)
            return BrokerAccountChange.Reject("BrokerAccount.ENVIRONMENT.CONFLICT");
        if (brokerAccountDefinition.Snapshot is { } prior && command.Snapshot.Generation < prior.Generation)
            return BrokerAccountChange.Unchanged(brokerAccountDefinition);
        if (brokerAccountDefinition.Snapshot == command.Snapshot)
            return BrokerAccountChange.Unchanged(brokerAccountDefinition);
        var changedAccount = brokerAccountDefinition with
        {
            Environment = command.Environment,
            Snapshot = command.Snapshot,
            Revision = brokerAccountDefinition.Revision + 1,
            ChangedAtUtc = command.Snapshot.AsOfUtc
        };
        changedAccount = changedAccount with { Gate = Gate(changedAccount) };
        return BrokerAccountChange.Accept(changedAccount);
    }

    /// <summary>Computes ReleaseManualTradingHold without changing account state.</summary>
    internal static BrokerAccountChange ReleaseManualTradingHold(ReleaseManualTradingHoldCommand command, BrokerAccountDefinition? currentAccount)
    {
        var brokerAccountDefinition = currentAccount;
        if (brokerAccountDefinition is null) return BrokerAccountChange.Reject("BrokerAccount.NOT_FOUND");
        if (!brokerAccountDefinition.ManualHold) return BrokerAccountChange.Unchanged(brokerAccountDefinition);
        var changedAccount = brokerAccountDefinition with
        {
            ManualHold = false,
            Reason = command.Reason,
            ChangedAtUtc = command.EffectiveAtUtc,
            Revision = brokerAccountDefinition.Revision + 1
        };
        changedAccount = changedAccount with { Gate = Gate(changedAccount) };
        return BrokerAccountChange.Accept(changedAccount);
    }

    /// <summary>Computes RevokeAccountQualification without changing account state.</summary>
    internal static BrokerAccountChange RevokeAccountQualification(RevokeAccountQualificationCommand command, BrokerAccountDefinition? currentAccount)
    {
        var brokerAccountDefinition = currentAccount;
        if (brokerAccountDefinition is null) return BrokerAccountChange.Reject("BrokerAccount.NOT_FOUND");
        if (brokerAccountDefinition.QualificationStatus == BrokerAccountQualificationStatus.Revoked &&
            brokerAccountDefinition.Reason == command.Reason)
            return BrokerAccountChange.Unchanged(brokerAccountDefinition);
        var changedAccount = brokerAccountDefinition with
        {
            QualificationStatus = BrokerAccountQualificationStatus.Revoked,
            Gate = BrokerAccountOperationalGate.Closed,
            AuthorizedBy = command.AuthorizedBy,
            Reason = command.Reason,
            ChangedAtUtc = command.RevokedAtUtc,
            Revision = brokerAccountDefinition.Revision + 1
        };
        return BrokerAccountChange.Accept(changedAccount);
    }

    /// <summary>Computes SetManualTradingHold without changing account state.</summary>
    internal static BrokerAccountChange SetManualTradingHold(SetManualTradingHoldCommand command, BrokerAccountDefinition? currentAccount)
    {
        var brokerAccountDefinition = currentAccount;
        if (brokerAccountDefinition is null) return BrokerAccountChange.Reject("BrokerAccount.NOT_FOUND");
        if (brokerAccountDefinition.ManualHold && brokerAccountDefinition.Reason == command.Reason)
            return BrokerAccountChange.Unchanged(brokerAccountDefinition);
        return BrokerAccountChange.Accept(brokerAccountDefinition with
        {
            ManualHold = true,
            Gate = BrokerAccountOperationalGate.Closed,
            Reason = command.Reason,
            ChangedAtUtc = command.EffectiveAtUtc,
            Revision = brokerAccountDefinition.Revision + 1
        });
    }

    /// <summary>Computes SubmitAccountQualificationEvidence without changing account state.</summary>
    internal static BrokerAccountChange SubmitAccountQualificationEvidence(SubmitAccountQualificationEvidenceCommand command, BrokerAccountDefinition? currentAccount)
    {
        var brokerAccountDefinition = currentAccount;
        if (brokerAccountDefinition?.Snapshot is null) return BrokerAccountChange.Reject("BrokerAccount.SNAPSHOT.REQUIRED");
        if (brokerAccountDefinition.ManifestHash == command.ManifestHash &&
            brokerAccountDefinition.QualificationStatus == BrokerAccountQualificationStatus.ReviewPending)
            return BrokerAccountChange.Unchanged(brokerAccountDefinition);
        var changedAccount = brokerAccountDefinition with
        {
            ManifestHash = command.ManifestHash,
            EvidenceReference = command.EvidenceReference,
            QualificationStatus = BrokerAccountQualificationStatus.ReviewPending,
            ApprovalId = Guid.Empty,
            AuthorizedBy = string.Empty,
            Gate = BrokerAccountOperationalGate.Closed,
            ChangedAtUtc = command.SubmittedAtUtc,
            Revision = brokerAccountDefinition.Revision + 1
        };
        return BrokerAccountChange.Accept(changedAccount);
    }

    /// <summary>Determines whether qualification, snapshot evidence, and manual hold permit new risk.</summary>
    internal static BrokerAccountOperationalGate Gate(BrokerAccountDefinition brokerAccountDefinition) =>
        brokerAccountDefinition.ManualHold || brokerAccountDefinition.QualificationStatus != BrokerAccountQualificationStatus.Accepted ||
        brokerAccountDefinition.Snapshot is not { Complete: true, NewRiskAllowed: true }
            ? BrokerAccountOperationalGate.Closed : BrokerAccountOperationalGate.Open;
}
