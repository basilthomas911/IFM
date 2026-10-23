using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class TradeFeedRecoveryApi(IMarketDataApi market) : ITradeFeedRecoveryApi
{
    public Task<bool> LoadAsync(TradeEntityId trade, DateOnly date, CancellationToken token = default)
        => market.LoadTradeFeedRecoveryAsync(trade.Format(), date, token);
    public Task<bool> EnableAsync(TradeEntityId trade, DateOnly date, CancellationToken token = default)
        => market.EnableTradeFeedRecoveryAsync(trade.Format(), date, token);
    public Task<bool> DisableAsync(TradeEntityId trade, DateOnly date, CancellationToken token = default)
        => market.DisableTradeFeedRecoveryAsync(trade.Format(), date, token);
}
