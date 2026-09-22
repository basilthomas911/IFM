using MessagePack;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Bounded maintenance observation, never an authorization to release consumed capacity.</summary>
[MessagePackObject]
public sealed record CapacityExpiryCandidate([property:Key(0)] int PortfolioId,[property:Key(1)] int FundId,
    [property:Key(2)] Guid ReservationId,[property:Key(3)] long ReservationVersion,[property:Key(4)] int Units,
    [property:Key(5)] string RequirementsHash,[property:Key(6)] long FinancialRevision,[property:Key(7)] DateTime ExpiredAtUtc);
