using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
namespace TomasAI.IFM.Application.Api.Server.Core.Development.Verification;
/// <summary>Requests historical seed bars from the market-data Host.</summary>
public sealed class FourHourDatabentoSeedReplay(IMarketDataApi marketDataApi)
{
    public Task<FuturesTradeSessionBarReadModel[]> ReadAsync(string contractId, CancellationToken cancellationToken)
        => marketDataApi.ReadFiveMinuteSeedBarsAsync(contractId, cancellationToken);
}
