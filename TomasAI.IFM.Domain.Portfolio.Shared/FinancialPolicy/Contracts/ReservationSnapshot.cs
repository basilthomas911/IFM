using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Authoritative lifecycle snapshot, including transitions committed by either capacity Function.</summary>
[MessagePackObject]
public sealed record ReservationSnapshot([property: Key(0)] Guid ReservationId, [property: Key(1)] long Version, [property: Key(2)] ReservationStatus Status,
    [property: Key(3)] int StrategyUnits, [property: Key(4)] int FilledUnits, [property: Key(5)] int CancelledUnits, [property: Key(6)] int RemainingUnits,
    [property: Key(7)] string RequirementsHash, [property: Key(8)] DateTime ValidUntilUtc, [property: Key(9)] Guid? ExecutionId, [property: Key(10)] long ExecutionRevision, [property: Key(11)] int ClosedUnits = 0);
