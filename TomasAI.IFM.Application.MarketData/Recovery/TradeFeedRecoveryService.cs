using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;

namespace TomasAI.IFM.Application.MarketData.Recovery;

public interface ITradeFeedRecoveryRuntime
{
    DateOnly? ActiveDate { get; }
    Task<EstablishedTradeDefinition?> LoadAsync(TradeEntityId trade, CancellationToken token);
    Task StartAsync(string owner, string contract, DateOnly date, CancellationToken token);
    Task StopAsync(string owner, string contract, CancellationToken token);
}

/// <summary>One restoration path for startup and trade loading. Serializes enable/off against restoration.</summary>
public sealed class TradeFeedRecoveryService(ITradeFeedRecoveryStore store, ITradeFeedRecoveryRuntime runtime, ILogger<TradeFeedRecoveryService>? logger = null)
{
    readonly SemaphoreSlim serial = new(1, 1);
    public static string Owner(string trade, DateOnly date) => "TradeFeedRecovery." + new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{trade}:{date:yyyy-MM-dd}")).AsSpan(0,16)).ToString("N");
    public Task<TradeFeedRecoveryRegistration[]> ReadAsync(DateOnly date, CancellationToken token) => store.ReadAsync(date, token);
    public async Task<bool> LoadAsync(string trade, DateOnly date, bool enable, CancellationToken token)
    {
        using var operation = new OptionFeedTrace("option.trade.enable_or_restore", logger, $"{trade}|{date:yyyy-MM-dd}");
        using (var wait = new OptionFeedTrace("option.trade.wait_serial", logger, trade))
        {
            await serial.WaitAsync(token);
            wait.Complete();
        }
        try
        {
            if (runtime.ActiveDate != date) { operation.Complete(); return false; }
            using var read = new OptionFeedTrace("option.trade.read_intent", logger, trade);
            var previous = (await store.ReadAsync(date, token)).SingleOrDefault(row => row.TradeId == trade);
            read.Complete();
            read.Dispose();
            if (!enable && previous is null) { operation.Complete(); return false; }
            // Always load the established trade before recreating subscriptions; never replay market or execution events.
            using var load = new OptionFeedTrace("option.trade.load_definition", logger, trade);
            var definition = await runtime.LoadAsync(TradeEntityId.Parse(trade), token);
            load.Complete();
            load.Dispose();
            if (definition?.Status != EstablishedTradeStatus.Open)
            {
                await store.RemoveAsync(trade, date, token);
                if (previous is not null) await StopAllAsync(previous.OptionContracts.Select(contract => (Owner(trade,date), contract)), token);
                operation.Complete();
                return false;
            }
            var contracts = definition.Legs.Select(leg => leg.ContractId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (definition.StrategyKind is not (TradeStrategyKind.IronCondor or TradeStrategyKind.VerticalSpread) || contracts.Length != definition.Legs.Length || contracts.Length == 0)
                throw new InvalidOperationException("Recovery requires an option trade with distinct contracts.");
            using (var save = new OptionFeedTrace("option.trade.save_intent", logger, trade))
            {
                await store.SaveAsync(new(trade, date, contracts, DateTimeOffset.UtcNow), token);
                save.Complete();
            }
            List<Exception> failures = [];
            if (previous is not null)
            {
                try { await StopAllAsync(previous.OptionContracts.Except(contracts).Select(contract => (Owner(trade,date), contract)), token); }
                catch (AggregateException error) { failures.AddRange(error.InnerExceptions); }
            }
            foreach (var contract in contracts)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var step = new OptionFeedTrace("option.trade.start_contract", logger, $"{trade}|{contract}|{date:yyyy-MM-dd}");
                    await runtime.StartAsync(Owner(trade,date), contract, date, token);
                    step.Complete();
                }
                catch (Exception error) when (!token.IsCancellationRequested) { failures.Add(error); }
            }
            if (failures.Count != 0) throw new AggregateException("Trade live-feed intent is saved, but some option leases could not be reconciled.", failures);
            operation.Complete();
            return true;
        }
        catch (Exception error) { operation.Fail(error); throw; }
        finally { serial.Release(); }
    }
    public async Task DisableAsync(string trade, DateOnly date, CancellationToken token)
    {
        await serial.WaitAsync(token);
        try
        {
            var previous = (await store.ReadAsync(date, token)).SingleOrDefault(row => row.TradeId == trade);
            await store.RemoveAsync(trade, date, token);
            if (previous is not null) await StopAllAsync(previous.OptionContracts.Select(contract => (Owner(trade,date), contract)), token);
        }
        finally { serial.Release(); }
    }
    public async Task CloseAsync(DateOnly date, CancellationToken token)
    {
        await serial.WaitAsync(token);
        try
        {
            var previous = await store.ReadAsync(date, token);
            await store.CloseAsync(date, token);
            await StopAllAsync(previous.SelectMany(row => row.OptionContracts.Select(contract => (Owner(row.TradeId,date), contract))), token);
        }
        finally { serial.Release(); }
    }
    async Task StopAllAsync(IEnumerable<(string Owner, string Contract)> leases, CancellationToken token)
    {
        List<Exception> failures = [];
        foreach (var lease in leases)
        {
            token.ThrowIfCancellationRequested();
            try { await runtime.StopAsync(lease.Owner, lease.Contract, token); }
            catch (Exception error) when (!token.IsCancellationRequested) { failures.Add(error); }
        }
        if (failures.Count != 0) throw new AggregateException("Recovery intent was removed, but some option leases could not be released.", failures);
    }
}
