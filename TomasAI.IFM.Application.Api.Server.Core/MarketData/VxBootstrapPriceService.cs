using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.Api.Server.Core.MarketData;

/// <summary>Restores the last VX observation asynchronously; never turns historical estimates into live-feed evidence.</summary>
public sealed class VxBootstrapPriceService(DatasetWorkerCurrentValues cache, DatasetDesiredSubscriptionRegistry subscriptions,
    IDbContextFactory storage, IMarketDataHistoricalProvider historical, ILogger<VxBootstrapPriceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset retryAt = default;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (cache.ActiveValueDate is { } date && DateTimeOffset.UtcNow >= retryAt)
                {
                    var vx = subscriptions.CaptureCurrentRecoverySnapshot(date, TimeProvider.System).Manifests
                        .SelectMany(manifest => manifest.Contracts).FirstOrDefault(contract => contract.RootSymbol == "VX" && contract.OnTheRun);
                    if (vx is not null && (!cache.TryGetLastTickPrice(vx.DomainContractId, out var current) || current.Trade is null))
                    {
                        retryAt = DateTimeOffset.UtcNow.AddMinutes(1);
                        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        deadline.CancelAfter(TimeSpan.FromSeconds(30));
                        var seed = await LoadAsync(vx, date, deadline.Token).ConfigureAwait(false);
                        if (seed is { } value && cache.SeedBootstrapPrice(date, value.Price, value.Source))
                            logger.LogInformation("VX bootstrap populated: Contract={Contract}, Price={Price}, EventUtc={EventUtc}, Source={Source}, ActiveDate={Date}. Historical estimate; live feed evidence unchanged.",
                                vx.DomainContractId, value.Price.Trade!.Value.LastPrice, value.Price.Trade.Value.EventTimestamp, value.Source, date);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning(exception, "VX bootstrap unavailable; live feed and recovery continue, retrying in one minute."); retryAt = DateTimeOffset.UtcNow.AddMinutes(1); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
        }
    }

    async Task<(FuturesMarketPriceSnapshot Price, string Source)?> LoadAsync(DatasetSubscriptionContract vx, DateOnly date, CancellationToken token)
    {
        try
        {
            var rows = await storage.TradeTickQuoteDb.GetLatestRawFuturesTradesAsync(vx.DomainContractId, date, token).ConfigureAwait(false);
            foreach (var row in rows.OrderByDescending(row => row.EventNanoseconds))
            {
                if (row.Price <= 0 || row.EventNanoseconds <= 0 || row.Size < 0 || row.Size > uint.MaxValue
                    || (row.HeaderFlags & 4) != 0 || row.Action is not (65 or 70 or 84 or 77)) continue;
                var trade = new FuturesMarketTradeSnapshot(row.Price, (uint)row.Size, row.Sequence,
                    DateTimeOffset.UnixEpoch.AddTicks(row.EventNanoseconds / 100), DateTimeOffset.UnixEpoch.AddTicks(row.ReceiveNanoseconds / 100),
                    row.Action == 77 ? NormalizedTradeAction.Change : NormalizedTradeAction.New,
                    NormalizedTradeConditionFlags: NormalizedTradeConditionFlags.Replay);
                if (trade.EventTimestamp > DateTimeOffset.UtcNow || trade.EventTimestamp < DateTimeOffset.UtcNow.AddDays(-7)) continue;
                return (new(vx.DomainContractId, row.InstrumentId, row.PublisherId, AssetTypeId.Futures, row.ValueDate, null, trade), "TradeTickQuoteDb");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { logger.LogWarning(exception, "VX persisted trade lookup failed; trying Databento history."); }
        // History can lag the live stream. Search bounded UTC day windows, newest first.
        var end = DateTimeOffset.UtcNow.AddMinutes(-20);
        for (var day = 0; day < 7; day++)
        {
            var start = end.AddDays(-1);
            try
            {
                var request = new HistoricalProviderRequest { Dataset = vx.Dataset, Symbols = [vx.ProviderContractName],
                    Schema = HistoricalDataSchema.Trades, Symbology = HistoricalSymbology.RawSymbol,
                    StartUtc = start, EndUtc = end, RecordLimit = 100001, RequestHash = $"VX-bootstrap|{vx.DomainContractId}|{start:O}|{end:O}" };
                var estimate = await historical.EstimateAsync(request, token).ConfigureAwait(false);
                if (estimate.EstimatedCostUsd > 0.10m) { logger.LogWarning("VX historical bootstrap exceeds the $0.10 request budget."); return null; }
                await using var reader = await historical.OpenRangeAsync(request, 1024, token).ConfigureAwait(false);
                HistoricalProviderRecord? latest = null;
                var count = 0;
                while (await reader.ReadNextAsync(token).ConfigureAwait(false) is { } batch)
                {
                    count += batch.Records.Count;
                    if (count > 100000) throw new InvalidOperationException("VX bootstrap historical window exceeds the bounded record limit.");
                    foreach (var record in batch.Records)
                        if (record.Kind == HistoricalProviderRecordKind.Trade && record.CloseOrPrice > 0
                            && record.VolumeOrSize >= 0 && record.VolumeOrSize <= uint.MaxValue
                            && record.Action is not (NormalizedTradeAction.Cancel or NormalizedTradeAction.Clear)
                            && (record.Conditions & NormalizedTradeConditionFlags.UndefinedPrice) == 0
                            && record.EventTimestampUtc < end && record.EventTimestampUtc >= start
                            && (latest is null || record.EventTimestampUtc > latest.EventTimestampUtc)) latest = record;
                }
                if (latest is not null)
                {
                    var trade = new FuturesMarketTradeSnapshot(latest.CloseOrPrice, (uint)latest.VolumeOrSize, latest.SourceSequence,
                        latest.EventTimestampUtc, latest.EventTimestampUtc, latest.Action, latest.Side, latest.Conditions | NormalizedTradeConditionFlags.Replay);
                    return (new(vx.DomainContractId, uint.Parse(latest.InstrumentId), ushort.Parse(latest.PublisherId), AssetTypeId.Futures,
                        DateOnly.FromDateTime(latest.EventTimestampUtc.UtcDateTime), null, trade), "DatabentoHistorical");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "VX historical window {Start} through {End} is unavailable; trying the preceding window.", start, end);
            }
            end = start;
        }
        logger.LogWarning("No VX trade within the seven-day bootstrap window for {Contract}.", vx.DomainContractId);
        return null;
    }
}

