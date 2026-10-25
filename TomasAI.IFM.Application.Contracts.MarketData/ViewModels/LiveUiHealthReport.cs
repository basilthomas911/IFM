namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

// Receipt and rendering are reported separately. A server timestamp is used for heartbeat expiry.
public sealed record LiveUiHealthReport(Guid SiteId, DateOnly ValueDate,
    IReadOnlyList<LiveUiStreamEvidence> Streams);
