using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

public sealed class OptionVolatilityService(IOptionVolatilityRepository repository)
    : IOptionVolatilityQueryApi, IOptionVolatilityPublicationService
{
    public Task PublishAsync(OptionIvPublication publication, CancellationToken cancellationToken = default) =>
        repository.PublishAsync(publication, cancellationToken);
    public Task<LatestVolatilityResult> GetLatestAsync(LatestVolatilityRequest request,
        CancellationToken cancellationToken = default) => repository.GetLatestAsync(request, cancellationToken);
    public Task<OptionIvMetricRevision?> GetSnapshotAsync(string environment, string snapshotId,
        CancellationToken cancellationToken = default) => repository.GetSnapshotAsync(environment, snapshotId, cancellationToken);
    public Task<VolatilityPage<OptionIvObservation>> GetObservationHistoryAsync(VolatilityHistoryPageRequest request,
        CancellationToken cancellationToken = default) => repository.GetObservationHistoryAsync(request, cancellationToken);
    public Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryAsync(VolatilityHistoryPageRequest request,
        CancellationToken cancellationToken = default) => repository.GetMetricHistoryAsync(request, cancellationToken);
}
