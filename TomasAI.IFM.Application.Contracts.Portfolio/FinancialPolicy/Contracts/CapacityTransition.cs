using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record CapacityTransition([property: Key(0)] ReservationStatus Status, [property: Key(1)] int FilledUnits, [property: Key(2)] int CancelledUnits,
    [property: Key(3)] int RemainingUnits, [property: Key(4)] decimal HeldFraction, [property: Key(5)] decimal WorkingFraction,
    [property: Key(6)] decimal PositionFraction, [property: Key(7)] int ClosedUnits = 0);
