using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

/// <summary>Audit evidence for the latest re-sizing decision; earlier decisions remain in committed snapshots.</summary>
[MessagePackObject]
public sealed record RiskResizeEvidence(
    [property: Key(0)] Guid PreviousInvocationId,
    [property: Key(1)] Guid PreviousReservationOperationId,
    [property: Key(2)] long PreviousExpectedFinancialRevision,
    [property: Key(3)] long AbsenceFinancialRevision,
    [property: Key(4)] long AdmissionFinancialRevision,
    [property: Key(5)] DateTime ObservedAtUtc);
