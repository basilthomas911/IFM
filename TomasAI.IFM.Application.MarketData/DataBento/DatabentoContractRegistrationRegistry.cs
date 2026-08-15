using System.Collections;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

public interface IDatabentoContractRegistrationRegistry :
    IReadOnlyList<DatabentoContractRegistration>
{
    IReadOnlyList<DatabentoContractRegistration> Snapshot();

    void ReplaceCurrentFuturesContracts(
        IReadOnlyCollection<FuturesContractV2ReadModel> contracts);
}

/// <summary>
/// Publishes immutable contract-registration snapshots to newly created
/// market-data epochs while allowing startup rollover reconciliation to replace
/// the current assignment for a futures root.
/// </summary>
public sealed class DatabentoContractRegistrationRegistry(
    IEnumerable<DatabentoContractRegistration> initialRegistrations,
    DatabentoMarketDataRuntimeOptions options)
    : IDatabentoContractRegistrationRegistry
{
    private DatabentoContractRegistration[] _registrations =
        Validate(initialRegistrations).ToArray();

    public int Count => Volatile.Read(ref _registrations).Length;
    public DatabentoContractRegistration this[int index] =>
        Volatile.Read(ref _registrations)[index];

    public IReadOnlyList<DatabentoContractRegistration> Snapshot() =>
        Volatile.Read(ref _registrations).ToArray();

    public void ReplaceCurrentFuturesContracts(
        IReadOnlyCollection<FuturesContractV2ReadModel> contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        if (contracts.Count == 0)
            throw new ArgumentException("At least one current futures contract is required.", nameof(contracts));
        if (contracts.Any(static contract => !contract.CurrentlyTraded))
            throw new ArgumentException("Every rollover registration must be currently traded.", nameof(contracts));

        var symbols = contracts.Select(static contract => contract.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var current = Volatile.Read(ref _registrations);
            var retained = current.Where(registration =>
                registration.AssetTypeId != AssetTypeId.Futures
                || !symbols.Contains(GetRootSymbol(registration)))
                .ToList();
            retained.AddRange(contracts.Select(contract => new DatabentoContractRegistration
            {
                DomainContractId = contract.ContractId,
                ProviderContractName = contract.LocalSymbol,
                AssetTypeId = AssetTypeId.Futures,
                RootSymbol = contract.Symbol,
                Dataset = DatabentoDatasetSelection.Resolve(options, contract.Symbol)
            }));
            var replacement = Validate(retained)
                .OrderBy(static registration => registration.DomainContractId, StringComparer.Ordinal)
                .ToArray();
            if (ReferenceEquals(
                Interlocked.CompareExchange(ref _registrations, replacement, current),
                current))
                return;
        }
    }

    public IEnumerator<DatabentoContractRegistration> GetEnumerator() =>
        ((IEnumerable<DatabentoContractRegistration>)Snapshot()).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static IEnumerable<DatabentoContractRegistration> Validate(
        IEnumerable<DatabentoContractRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var snapshot = registrations.ToArray();
        if (snapshot.Any(static registration =>
            string.IsNullOrWhiteSpace(registration.DomainContractId)
            || string.IsNullOrWhiteSpace(registration.ProviderContractName)))
            throw new ArgumentException("Contract registrations require domain and provider identifiers.");
        if (snapshot.Select(static registration => registration.DomainContractId)
            .Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new ArgumentException("Domain contract IDs must be unique.");
        return snapshot;
    }

    private static string GetRootSymbol(DatabentoContractRegistration registration)
    {
        if (!string.IsNullOrWhiteSpace(registration.RootSymbol))
            return registration.RootSymbol;
        try { return new FuturesContractIdParser(registration.DomainContractId).Symbol; }
        catch { return string.Empty; }
    }
}

internal static class DatabentoDatasetSelection
{
    internal static string Resolve(
        DatabentoMarketDataRuntimeOptions options,
        string symbol)
    {
        if (options.FuturesContractDatasets.TryGetValue(symbol, out var configured)
            && !string.IsNullOrWhiteSpace(configured))
            return configured;
        return string.Equals(symbol, "VX", StringComparison.OrdinalIgnoreCase)
            ? "XCBF.PITCH"
            : options.FeedOptions.Dataset;
    }

    internal static string Resolve(
        DatabentoMarketDataRuntimeOptions options,
        DatabentoContractRegistration registration) =>
        !string.IsNullOrWhiteSpace(registration.Dataset)
            ? registration.Dataset
            : options.FeedOptions.Dataset;
}
