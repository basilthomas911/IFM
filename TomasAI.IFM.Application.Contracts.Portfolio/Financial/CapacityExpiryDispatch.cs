namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Describes a durable capacity-expiry command awaiting dispatch.</summary>
/// <param name="FundId">The affected fund identifier.</param>
/// <param name="Request">The capacity-change command.</param>
public sealed record CapacityExpiryDispatch(int FundId, ChangeCapacityReservationCommand Request);
