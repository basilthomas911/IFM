using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.ScheduledTask.SetClosingPrice;

public sealed class Worker(
    IHostApplicationLifetime lifetime,
    ScheduledTaskOutcome outcome,
    ILogger<Worker> logger,
    IActorProducer actorProducer,
    IMarketDataFeedCommandApi marketDataFeedCommandApi,
    ITradePlacementCommandApi tradePlacementCommandApi,
    IMarketDataFeedQueryApi marketDataFeedQueryApi,
    IMarketDataQueryApi marketDataQueryApi,
    IConfiguration configuration)
    : OneShotScheduledTaskWorker(lifetime, outcome, logger)
{
    protected override async Task ExecuteTaskAsync(CancellationToken cancellationToken)
    {
        await actorProducer.StartAsync(new ActorMailboxId(ActorType.Query, "SetClosingPrice"), cancellationToken).ConfigureAwait(false);
        try
        {
            var valueDateResult = await marketDataQueryApi.GetValueDateAsync().ConfigureAwait(false);
            if (!valueDateResult.Success || valueDateResult.Value is null)
            {
                throw new InvalidOperationException($"Unable to load value date: {valueDateResult.ErrorMessage}");
            }

            var valueDate = valueDateResult.Value.Value;
            var closeTime = TimeOnly.Parse(configuration["MarketSession:CloseTime"] ?? "16:00");
            var timeZoneId = configuration["MarketSession:TimeZone"] ?? "America/New_York";
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var symbols = configuration.GetSection("MarketSession:Symbols").Get<string[]>() ?? ["ES"];
            if (symbols.Length == 0)
            {
                throw new InvalidOperationException("At least one futures symbol must be configured.");
            }

            var failures = new List<string>();
            foreach (var symbol in symbols.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var contractsResult = await marketDataQueryApi.GetRolloverFuturesContractsAsync(symbol).ConfigureAwait(false);
                if (!contractsResult.Success || contractsResult.Value is null || contractsResult.Value.Length == 0)
                {
                    failures.Add($"{symbol}: no traded contracts ({contractsResult.ErrorMessage})");
                    continue;
                }

                foreach (var contract in contractsResult.Value)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var localClose = valueDate.ToDateTime(closeTime, DateTimeKind.Unspecified);
                    var tickResult = await marketDataFeedQueryApi.GetLastFuturesTickDataAsync(contract.ContractId, localClose).ConfigureAwait(false);
                    if (!tickResult.Success || tickResult.Value is null)
                    {
                        failures.Add($"{contract.ContractId}: closing tick unavailable ({tickResult.ErrorMessage})");
                        continue;
                    }

                    var closingPrice = tickResult.Value.Price;
                    var insert = await marketDataFeedCommandApi
                        .InsertFuturesClosingPriceAsync(new FuturesDataId(contract.ContractId, valueDate), closingPrice)
                        .ConfigureAwait(false);
                    if (!insert.Success)
                    {
                        failures.Add($"{contract.ContractId}: closing price rejected ({insert.ErrorMessage})");
                        continue;
                    }

                    logger.LogInformation("Closing price command {CommandId} accepted for {ContractId} at {Price}.", insert.Value, contract.ContractId, closingPrice);
                    if (contract.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase))
                    {
                        var stop = await tradePlacementCommandApi
                            .StopTradePlacementAsync(new TradePlacementId(contract.ContractId, valueDate))
                            .ConfigureAwait(false);
                        if (!stop.Success)
                        {
                            failures.Add($"{contract.ContractId}: trade-placement stop rejected ({stop.ErrorMessage})");
                        }
                    }
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException($"Set Closing Price completed with {failures.Count} failure(s): {string.Join("; ", failures)}");
            }
        }
        finally
        {
            await actorProducer.StopAsync().ConfigureAwait(false);
        }
    }
}
