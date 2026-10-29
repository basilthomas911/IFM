using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Preserved cold rollover persistence policy over native-selected
/// contract facts. This helper owns database values only, never a live dataset,
/// pricing context, managed epoch, subscription, lease or renewal task. A stalled
/// database request cannot delay Rust dataset cancellation, join or rebuild.</summary>
public sealed class NativeColdRolloverOperations(
    IDatabentoCurrentFuturesContractResolver resolver,IFuturesContractRolloverStore store,
    IDatabentoContractRegistrationRegistry registry,TimeProvider clock)
{
    readonly IDatabentoCurrentFuturesContractResolver _currentContractResolver=resolver;
    readonly IFuturesContractRolloverStore _rolloverStore=store;
    readonly IDatabentoContractRegistrationRegistry _contractRegistry=registry;
    readonly TimeProvider _timeProvider=clock;
    public async Task<bool> UpdateFuturesTermStructureContractsAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ValidateDate(valueDate, nameof(valueDate));
        var resolver = _currentContractResolver;
        var registry = _contractRegistry;
        var store = _rolloverStore;
        if (resolver is null || store is null) return false;
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var rollover = await store.GetFuturesContractRolloverAsync(
            normalizedSymbol, cancellationToken).ConfigureAwait(false);
        if (rollover?.ContractId is { Length: > 0 }
            && rollover.NextRolloverDate is { } rolloverDate
            && valueDate < rolloverDate)
        {
            var persisted = (await store.GetFuturesRolloverSetAsync(
                    normalizedSymbol, cancellationToken).ConfigureAwait(false))
                .OrderBy(static contract => contract.LastTradeDate)
                .ToArray();
            if (persisted.Length == 2
                && persisted.All(static contract => contract.Rollover)
                && persisted.All(contract => IsConsistentFuturesContract(contract, normalizedSymbol))
                && persisted.All(contract => contract.LastTradeDate > valueDate)
                && persisted.Count(static contract => contract.OnTheRun) == 1
                && persisted[0].OnTheRun
                && string.Equals(persisted[0].ContractId, rollover.ContractId, StringComparison.Ordinal))
            {
                registry?.ReplaceFuturesRolloverSet(normalizedSymbol, persisted);
                return false;
            }
        }

        var resolved = await resolver.ResolveEligibleAsync(
            normalizedSymbol, valueDate, 2, cancellationToken).ConfigureAwait(false);
        if (resolved.Count < 2) return false;
        var ordered = resolved
            .Where(contract => string.Equals(contract.Symbol, normalizedSymbol, StringComparison.Ordinal))
            .OrderBy(static contract => contract.LastTradeDate)
            .DistinctBy(static contract => contract.ContractId, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        if (ordered.Length != 2
            || ordered.Any(static contract => !contract.Rollover)
            || ordered.Count(static contract => contract.OnTheRun) != 1
            || !ordered[0].OnTheRun)
            return false;
        if (rollover is null)
        {
            throw new FuturesContractRolloverConfigurationException(
                $"The futures-contract rollover row for '{normalizedSymbol}' is missing.");
        }
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var replacement = rollover with
        {
            ContractId = ordered[0].ContractId,
            NextRolloverDate = ordered[0].LastTradeDate,
            UpdatedOn = now,
            UpdatedBy = nameof(DatabentoMarketDataApi)
        };
        await store.ReplaceFuturesRolloverSetAsync(
            replacement, ordered, cancellationToken).ConfigureAwait(false);
        registry?.ReplaceFuturesRolloverSet(normalizedSymbol, ordered);
        return true;
    }

    public async Task<bool> UpdateOnTheRunFuturesContractAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default,
        bool forceProviderRefresh = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ValidateDate(valueDate, nameof(valueDate));
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var store = _rolloverStore ?? throw new FuturesContractRolloverConfigurationException(
            "The futures-contract rollover store is not registered.");
        var resolver = _currentContractResolver ?? throw new FuturesContractRolloverConfigurationException(
            "The DataBento current-futures-contract resolver is not registered.");
        var existing = await store.GetFuturesContractRolloverAsync(
            normalizedSymbol, cancellationToken).ConfigureAwait(false)
            ?? throw new FuturesContractRolloverConfigurationException(
                $"The futures-contract rollover row for '{normalizedSymbol}' is missing.");

        if (!forceProviderRefresh
            && !string.IsNullOrWhiteSpace(existing.ContractId)
            && existing.NextRolloverDate is { } currentRolloverDate
            && valueDate < currentRolloverDate)
        {
            var persisted = await store.GetPersistedFuturesContractAsync(
                existing.ContractId,
                cancellationToken).ConfigureAwait(false);
            if (persisted is not null
                && persisted.OnTheRun
                && persisted.Rollover
                && persisted.LastTradeDate > valueDate
                && IsConsistentFuturesContract(persisted, normalizedSymbol)
                && string.Equals(
                    persisted.ContractId,
                    existing.ContractId,
                    StringComparison.Ordinal)
                && string.Equals(
                    persisted.Symbol,
                    normalizedSymbol,
                    StringComparison.Ordinal))
            {
                _contractRegistry?.ReplaceFuturesRolloverSet(normalizedSymbol, [persisted]);
                return false;
            }
        }

        var resolved = await resolver.ResolveAsync(
            normalizedSymbol, valueDate, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(resolved.Contract.Symbol, normalizedSymbol, StringComparison.Ordinal))
        {
            throw new MarketDataContractMappingException(
                resolved.Contract.ContractId,
                $"the resolved symbol '{resolved.Contract.Symbol}' does not match '{normalizedSymbol}'");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var replacement = existing with
        {
            ContractId = resolved.Contract.ContractId,
            NextRolloverDate = resolved.NextRolloverDate,
            UpdatedOn = now,
            UpdatedBy = nameof(DatabentoMarketDataApi)
        };
        await store.ReplaceOnTheRunFuturesContractAsync(
            replacement, resolved.Contract, cancellationToken).ConfigureAwait(false);
        _contractRegistry?.ReplaceFuturesRolloverSet(normalizedSymbol, [resolved.Contract]);
        return existing.NextRolloverDate != resolved.NextRolloverDate;
    }

    static bool IsConsistentFuturesContract(
        FuturesContractV3ReadModel contract,
        string expectedSymbol)
    {
        try
        {
            var identity = new FuturesContractIdParser(contract.ContractId);
            return string.Equals(identity.Symbol, expectedSymbol, StringComparison.Ordinal)
                && identity.MaturityDate == contract.LastTradeDate
                && contract.LocalSymbol.StartsWith(expectedSymbol, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void ValidateDate(DateOnly value, string parameterName)
    {
        if (value == default) throw new ArgumentOutOfRangeException(parameterName);
    }
}
