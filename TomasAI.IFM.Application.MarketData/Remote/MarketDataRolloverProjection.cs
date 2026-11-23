using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Projects the active native session's accepted contract set through the securities catalog actor.</summary>
public sealed class MarketDataRolloverProjection(IFuturesContractRolloverStore store, TimeProvider clock)
{
    readonly Dictionary<string, (DateOnly Date, FuturesContractV3ReadModel[] Contracts)> applied = new(StringComparer.Ordinal);

    /// <summary>Reconciles detached healthy current-session facts, never next-session preparation or guessed expiries.</summary>
    public async Task ApplyAsync(MarketDataReadSnapshot snapshot, CancellationToken token)
    {
        if (!snapshot.Status.Ready || snapshot.Status.ValueDate is not { } date) return;
        foreach (var symbol in new[] { "ES", "VX" })
        {
            var dataset = symbol == "VX" ? "XCBF.PITCH" : "GLBX.MDP3";
            if (!snapshot.Status.Datasets.Any(row => row.Role == "Futures" && row.Dataset == dataset
                && row.ValueDate == date && row.Running && row.Healthy && row.Lifecycle == "Healthy")) continue;
            var contracts = snapshot.Contracts.Where(row => row.Symbol == symbol)
                .OrderBy(row => row.LastTradeDate).ThenBy(row => row.ContractId, StringComparer.Ordinal).ToArray();
            if (contracts.Length != (symbol == "VX" ? 2 : 1) || !contracts[0].OnTheRun
                || contracts.Count(row => row.OnTheRun) != 1 || contracts.Any(row => !row.Rollover
                    || string.IsNullOrWhiteSpace(row.ContractId) || row.LastTradeDate <= date)
                || contracts.Select(row => row.ContractId).Distinct(StringComparer.Ordinal).Count() != contracts.Length)
                throw new InvalidDataException($"Invalid active native rollover set for {symbol}.");
            if (applied.TryGetValue(symbol, out var previous) && previous.Date == date
                && previous.Contracts.SequenceEqual(contracts)) continue;
            var now = clock.GetUtcNow().UtcDateTime;
            await store.EnsureFuturesContractRolloverRowsAsync([symbol], now, nameof(MarketDataRolloverProjection), token).ConfigureAwait(false);
            var pointer = await store.GetFuturesContractRolloverAsync(symbol, token).ConfigureAwait(false)
                ?? throw new InvalidDataException($"Missing rollover authority for {symbol}.");
            await store.ReplaceFuturesRolloverSetAsync(pointer with
            {
                ContractId = contracts[0].ContractId, NextRolloverDate = contracts[0].LastTradeDate,
                UpdatedOn = now, UpdatedBy = nameof(MarketDataRolloverProjection)
            }, contracts, token).ConfigureAwait(false);
            applied[symbol] = (date, contracts);
        }
    }
}
