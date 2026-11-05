using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Persistence;

public sealed record PortfolioEventMetadata(Guid CorrelationId, Guid CausationId, DateTime OriginatedOnUtc)
{
    /// <summary>Optional prepared Portfolio revision that must still be current when a Fund write commits.</summary>
    public long? ExpectedPortfolioRevision { get; init; }
    /// <summary>Exact projected Portfolio business version used during preparation.</summary>
    public long? ExpectedPortfolioVersion { get; init; }
    /// <summary>Exact projected Portfolio operating state used during preparation.</summary>
    public PortfolioOperatingState? ExpectedPortfolioOperatingState { get; init; }
    public PortfolioPreparedIdentifiers? PreparedIdentifiers { get; init; }
    public static PortfolioEventMetadata ForCommand(Guid commandId, Guid eventId, DateTime occurredOnUtc) =>
        new(commandId, eventId, occurredOnUtc);

    public void Validate()
    {
        if (CorrelationId == Guid.Empty) throw new InvalidOperationException("CorrelationId is required.");
        if (CausationId == Guid.Empty) throw new InvalidOperationException("CausationId is required.");
        if (OriginatedOnUtc.Kind != DateTimeKind.Utc) throw new InvalidOperationException("OriginatedOnUtc must be UTC.");
        if (ExpectedPortfolioRevision is <= 0) throw new InvalidOperationException("Prepared Portfolio revision must be positive.");
        if ((ExpectedPortfolioVersion is not null || ExpectedPortfolioOperatingState is not null)
            && (ExpectedPortfolioRevision is not > 0 || ExpectedPortfolioVersion is not > 0
                || ExpectedPortfolioOperatingState is null || !Enum.IsDefined(ExpectedPortfolioOperatingState.Value)))
            throw new InvalidOperationException("Prepared Portfolio authority requires an exact revision, version and operating state.");
    }
}
