using TomasAI.IFM.Domain.Trade.Shared.Recovery;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public partial interface IMarketDataApi
{
    [MarketDataHostOperation]
    Task<bool> CloseTradeFeedRecoveryAsync(DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromException<bool>(new NotSupportedException("Trade feed recovery is not configured."));
    [MarketDataHostOperation]
    Task<TradeFeedRecoveryRegistration[]> ReadTradeFeedRecoveryAsync(DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromException<TradeFeedRecoveryRegistration[]>(new NotSupportedException("Trade feed recovery is not configured."));
    [MarketDataHostOperation]
    Task<bool> LoadTradeFeedRecoveryAsync(string tradeId, DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromException<bool>(new NotSupportedException("Trade feed recovery is not configured."));
    [MarketDataHostOperation]
    Task<bool> EnableTradeFeedRecoveryAsync(string tradeId, DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromException<bool>(new NotSupportedException("Trade feed recovery is not configured."));
    [MarketDataHostOperation]
    Task<bool> DisableTradeFeedRecoveryAsync(string tradeId, DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromException<bool>(new NotSupportedException("Trade feed recovery is not configured."));
}
