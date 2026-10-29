// Application persistence/reference policy adapted from the current Host.
// Provider acquisition is supplied by the native IMarketDataApi composition.
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage;

namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Adapts the historical-load persistence port to the MarketDataDb context capabilities.</summary>
/// <param name="dbFactory">The factory providing the MarketDataDb context.</param>
public sealed class HistoricalDataLoaderStore(IDbContextFactory dbFactory) : IHistoricalDataLoaderStore
{
    /// <inheritdoc />
    public ValueTask<HistoricalDataLoaderState?> GetAsync(Guid attemptId, CancellationToken cancellationToken) =>
        new(dbFactory.MarketDataDb.GetHistoricalDataLoadAsync(attemptId, cancellationToken));

    /// <inheritdoc />
    public ValueTask<HistoricalDataLoaderState?> GetCompletedByRequestHashAsync(string requestSha256, CancellationToken cancellationToken) =>
        new(dbFactory.MarketDataDb.GetCompletedHistoricalDataLoadAsync(requestSha256, cancellationToken));

    /// <inheritdoc />
    public ValueTask SaveAsync(HistoricalDataLoaderState state, CancellationToken cancellationToken) =>
        new(dbFactory.MarketDataDb.SaveHistoricalDataLoadAsync(state, cancellationToken));
}
