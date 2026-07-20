using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.IntegrationTests.QueryApiResults;

/// <summary>
/// Provides methods for creating API result objects for option pricer-related queries.
/// </summary>
/// <remarks>This class contains static methods to generate specific result objects for various option pricer
/// queries, such as retrieving available devices, spread distributions, and job progress status. The methods are
/// designed to populate the response with pre-defined view models.</remarks>
public static class OptionPricerQueryApiResult
{
    public static Task FromGetOptionPricerDevicesAsync(HttpResponse resp)
        => resp.SetResult(new OptionPricerDevicesReadModel
        {
            Devices = new[] {
                new OptionPricerDeviceReadModel(deviceId: 0, deviceName: "LocalGPU", spreadPaths: 16, volatilityPaths: 16, maxBatchSize: 100, optionType: OptionType.Put, enabled: true),
                new OptionPricerDeviceReadModel(deviceId: 1, deviceName: "RemoteCPU", spreadPaths: 12, volatilityPaths: 12, maxBatchSize: 50, optionType: OptionType.Call, enabled: false)
            }
        });

    public static Task FromGetSpreadDistributionAsync(HttpResponse resp)
        => resp.SetResult(new SpreadDistributionReadModel(
            id: 1,
            tradeId: 1,
            valueDate: new DateOnly(2025, 10, 10),
            tradeType: TradeType.ShortPut,
            tradeStatus: TradeStatus.Open,
            daysToExpiry: 30,
            forwardPrice: 4500.25,
            lossProbability: 0.05,
            lossThreshold: 0.02m,
            lossThresholdCount: 10,
            shortVolatility: 0.2,
            longVolatility: 0.25,
            forwardLossRatio: 1.5,
            createdOn: System.DateTime.Now));

    public static Task FromIsSpreadDistributionJobInProgressAsync(HttpResponse resp)
        => resp.SetResult(new ScalarReadModel<bool>(false));
}
