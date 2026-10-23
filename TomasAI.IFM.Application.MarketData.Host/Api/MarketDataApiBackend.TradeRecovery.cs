using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Host;

public sealed partial class MarketDataApiBackend
{
    TradeFeedRecoveryService? tradeRecovery;
    IServiceProvider RecoveryServices => services;
    TradeFeedRecoveryService TradeRecovery => LazyInitializer.EnsureInitialized(ref tradeRecovery,
        () => new(services.GetRequiredService<ITradeFeedRecoveryStore>(), new RecoveryRuntime(this), services.GetService<Microsoft.Extensions.Logging.ILogger<TradeFeedRecoveryService>>()))!;

    sealed class RecoveryRuntime(MarketDataApiBackend backend) : ITradeFeedRecoveryRuntime
    {
        public DateOnly? ActiveDate => backend.activeDate;
        IDbContextFactory Databases => backend.RecoveryServices.GetRequiredService<IDbContextFactory>();
        public Task<EstablishedTradeDefinition?> LoadAsync(TradeEntityId trade, CancellationToken token)
            => Databases.TradeDb.GetEstablishedTradeAsync(trade, token);
        public async Task StartAsync(string owner, string contract, DateOnly date, CancellationToken token)
        {
            var definition = await Databases.SecuritiesDb.GetFuturesOptionContractAsync(contract, token)
                ?? throw new InvalidOperationException("Recovery option contract not found: " + contract);
            await backend.EnsureOptionsAsync(date, token);
            var feeds = backend.RecoveryServices.GetRequiredService<QualifiedIndividualOptionFeeds>();
            await feeds.AcquireAsync(LeaseOwner(feeds, owner, contract), definition, date).WaitAsync(token);
        }
        public Task StopAsync(string owner, string contract, CancellationToken token)
        {
            var feeds = backend.RecoveryServices.GetRequiredService<QualifiedIndividualOptionFeeds>();
            return feeds.ReleaseAsync(LeaseOwner(feeds, owner, contract), contract).WaitAsync(token);
        }
        static TickerStreamOwner LeaseOwner(QualifiedIndividualOptionFeeds feeds, string owner, string contract)
            => new("TradeFeedRecovery", owner + "." + feeds.OwnershipGeneration.ToString("N"), contract);
    }
}
