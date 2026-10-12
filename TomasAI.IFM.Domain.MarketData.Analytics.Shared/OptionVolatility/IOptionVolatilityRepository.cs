using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public interface IOptionVolatilityRepository
{
    Task AppendObservationAsync(string environment, OptionIvObservation observation,
        CancellationToken cancellationToken = default);
    Task PublishAsync(OptionIvPublication publication,
        CancellationToken cancellationToken = default);
    Task<OptionIvMetricRevision?> GetSnapshotAsync(string environment, string snapshotId,
        CancellationToken cancellationToken = default);
    Task<LatestVolatilityResult> GetLatestAsync(LatestVolatilityRequest request,
        CancellationToken cancellationToken = default);
    Task<VolatilityPage<OptionIvObservation>> GetObservationHistoryAsync(
        VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default);
    Task<VolatilityPage<OptionIvMetricRevision>> GetMetricHistoryAsync(
        VolatilityHistoryPageRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<VolatilityStorageScope, OptionIvLatestPointer>> RebuildLatestCacheAsync(
        IEnumerable<VolatilityStorageScope> scopes, CancellationToken cancellationToken = default);
}
