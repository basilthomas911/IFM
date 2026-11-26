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
        if (brokerAccountDefinition.Snapshot is { } prior)
        {
            if(command.Snapshot.Generation<prior.Generation)return BrokerAccountChange.Unchanged(brokerAccountDefinition);
            if(command.Snapshot.Generation==prior.Generation)
            {
                // Ordered broker revisions take precedence when both sides carry them; old evidence uses source time.
                var order=command.Snapshot.SourceSequence>0 && prior.SourceSequence>0
                    ? command.Snapshot.SourceSequence.CompareTo(prior.SourceSequence) : command.Snapshot.AsOfUtc.CompareTo(prior.AsOfUtc);
                if(order<0)return BrokerAccountChange.Unchanged(brokerAccountDefinition);
                if(order==0)
                {
                    if(Equivalent(prior,command.Snapshot))return BrokerAccountChange.Unchanged(brokerAccountDefinition);
                    if(prior.SourceSequence==0 && command.Snapshot.SourceSequence>0
                        && Equivalent(prior with{SourceSequence=command.Snapshot.SourceSequence},command.Snapshot))
                    {
                        var ordered=brokerAccountDefinition with{Snapshot=prior with{SourceSequence=command.Snapshot.SourceSequence},Revision=brokerAccountDefinition.Revision+1};
                        return BrokerAccountChange.Accept(ordered with{Gate=Gate(ordered)});
                    }
                    // Same-authority contradictory evidence may never reopen risk. Keep the established money/hold evidence.
                    if(!prior.NewRiskAllowed && !(prior.SourceSequence==0 && command.Snapshot.SourceSequence>0))return BrokerAccountChange.Unchanged(brokerAccountDefinition);
                    var conflicted=brokerAccountDefinition with { Snapshot=prior with{NewRiskAllowed=false,SourceSequence=Math.Max(prior.SourceSequence,command.Snapshot.SourceSequence)},Revision=brokerAccountDefinition.Revision+1 };
                    return BrokerAccountChange.Accept(conflicted with{Gate=Gate(conflicted)});
                }
            }
        }
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
            string.Equals(brokerAccountDefinition.Reason,command.Reason,StringComparison.Ordinal))
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
        if (brokerAccountDefinition.ManualHold && string.Equals(brokerAccountDefinition.Reason,command.Reason,StringComparison.Ordinal))
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
        if (string.Equals(brokerAccountDefinition.ManifestHash,command.ManifestHash,StringComparison.Ordinal) &&
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

    static bool Equivalent(BrokerAccountSnapshotEvidence x,BrokerAccountSnapshotEvidence y)=>x.AccountAlias==y.AccountAlias && x.Currency==y.Currency
        && x.CashBalance==y.CashBalance && x.AvailableFunds==y.AvailableFunds && x.Complete==y.Complete && x.NewRiskAllowed==y.NewRiskAllowed
        && x.Generation==y.Generation && x.SourceSequence==y.SourceSequence && x.Positions.SequenceEqual(y.Positions);

    /// <summary>Determines whether qualification, snapshot evidence, and manual hold permit new risk.</summary>
    internal static BrokerAccountOperationalGate Gate(BrokerAccountDefinition brokerAccountDefinition) =>
        brokerAccountDefinition.ManualHold || brokerAccountDefinition.QualificationStatus != BrokerAccountQualificationStatus.Accepted ||
        brokerAccountDefinition.Snapshot is not { Complete: true, NewRiskAllowed: true }
            ? BrokerAccountOperationalGate.Closed : BrokerAccountOperationalGate.Open;
}
