using TomasAI.IFM.Domain.Trade.Shared.Recovery;

namespace TomasAI.IFM.Application.MarketData.Remote;

public partial class MarketDataApiClient
{
    public Task<bool> CloseTradeFeedRecoveryAsync(DateOnly date, CancellationToken cancellationToken = default)
        => CallAsync<bool>(nameof(CloseTradeFeedRecoveryAsync), [typeof(DateOnly), typeof(CancellationToken)], [date, null], cancellationToken);
    public Task<TradeFeedRecoveryRegistration[]> ReadTradeFeedRecoveryAsync(DateOnly date, CancellationToken cancellationToken = default)
        => CallAsync<TradeFeedRecoveryRegistration[]>(nameof(ReadTradeFeedRecoveryAsync), [typeof(DateOnly), typeof(CancellationToken)], [date, null], cancellationToken);
    public Task<bool> LoadTradeFeedRecoveryAsync(string tradeId, DateOnly date, CancellationToken cancellationToken = default)
        => CallAsync<bool>(nameof(LoadTradeFeedRecoveryAsync), [typeof(string), typeof(DateOnly), typeof(CancellationToken)], [tradeId, date, null], cancellationToken);
    public Task<bool> EnableTradeFeedRecoveryAsync(string tradeId, DateOnly date, CancellationToken cancellationToken = default)
        => CallAsync<bool>(nameof(EnableTradeFeedRecoveryAsync), [typeof(string), typeof(DateOnly), typeof(CancellationToken)], [tradeId, date, null], cancellationToken);
    public Task<bool> DisableTradeFeedRecoveryAsync(string tradeId, DateOnly date, CancellationToken cancellationToken = default)
        => CallAsync<bool>(nameof(DisableTradeFeedRecoveryAsync), [typeof(string), typeof(DateOnly), typeof(CancellationToken)], [tradeId, date, null], cancellationToken);
}
