using System.Collections;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento;

public interface IDatabentoContractRegistrationRegistry :
    IReadOnlyList<DatabentoContractRegistration>
{
    IReadOnlyList<DatabentoContractRegistration> Snapshot();

    bool TryGetOnTheRunFuturesContract(
        string symbol,
        out FuturesContractV3ReadModel contract);

    bool TryGetFuturesTermStructureContracts(
        string symbol,
        out FuturesTermStructureContracts contracts);

    void ReplaceFuturesRolloverSet(
        string symbol,
        IReadOnlyCollection<FuturesContractV3ReadModel> contracts);
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
    private RegistryState _state = new(
        Validate(initialRegistrations).ToArray(),
        new Dictionary<string, FuturesContractV3ReadModel>(
            StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, FuturesTermStructureContracts>(
            StringComparer.OrdinalIgnoreCase));

    public int Count => Volatile.Read(ref _state).Registrations.Length;
    public DatabentoContractRegistration this[int index] =>
        Volatile.Read(ref _state).Registrations[index];

    public IReadOnlyList<DatabentoContractRegistration> Snapshot() =>
        Volatile.Read(ref _state).Registrations.ToArray();

    public bool TryGetOnTheRunFuturesContract(
        string symbol,
        out FuturesContractV3ReadModel contract)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return Volatile.Read(ref _state).OnTheRunFuturesContracts.TryGetValue(
            symbol.Trim(),
            out contract!);
    }

    public bool TryGetFuturesTermStructureContracts(
        string symbol,
        out FuturesTermStructureContracts contracts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return Volatile.Read(ref _state).TermStructures.TryGetValue(symbol.Trim(), out contracts);
    }

    public void ReplaceFuturesRolloverSet(
        string symbol,
        IReadOnlyCollection<FuturesContractV3ReadModel> contracts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(contracts);
        var normalized = symbol.Trim().ToUpperInvariant();
        var ordered = contracts.OrderBy(static contract => contract.LastTradeDate).ToArray();
        if (ordered.Length == 0
            || ordered.Any(contract => !contract.IsValid
                || !contract.Rollover
                || !string.Equals(contract.Symbol, normalized, StringComparison.OrdinalIgnoreCase))
            || ordered.Select(static contract => contract.ContractId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || ordered.Count(static contract => contract.OnTheRun) != 1
            || !ordered[0].OnTheRun)
        {
            throw new ArgumentException(
                "A rollover set requires one expiry-ordered on-the-run front and rollover-enabled distinct contracts for one root.",
                nameof(contracts));
        }
        if (string.Equals(normalized, "VX", StringComparison.Ordinal) && ordered.Length != 2)
            throw new ArgumentException("The VX rollover set requires exactly front and back contracts.", nameof(contracts));
        if (string.Equals(normalized, "ES", StringComparison.Ordinal) && ordered.Length != 1)
            throw new ArgumentException("The ES rollover set requires exactly one quarterly contract in v1.", nameof(contracts));

        while (true)
        {
            var current = Volatile.Read(ref _state);
            var retained = current.Registrations.Where(registration =>
                registration.AssetTypeId != AssetTypeId.Futures
                || !string.Equals(GetRootSymbol(registration), normalized,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            retained.AddRange(ordered.Select(contract =>
                new DatabentoContractRegistration
                {
                    DomainContractId = contract.ContractId,
                    ProviderContractName = contract.LocalSymbol,
                    AssetTypeId = AssetTypeId.Futures,
                    RootSymbol = normalized,
                    Dataset = DatabentoDatasetSelection.Resolve(options, normalized),
                    OnTheRun = contract.OnTheRun,
                    Rollover = contract.Rollover
                }));
            var structures = current.TermStructures.ToDictionary(
                static item => item.Key, static item => item.Value,
                StringComparer.OrdinalIgnoreCase);
            if (ordered.Length == 2)
                structures[normalized] = new FuturesTermStructureContracts(ordered[0], ordered[1]);
            else
                structures.Remove(normalized);
            var currentContracts = current.OnTheRunFuturesContracts.ToDictionary(
                static item => item.Key, static item => item.Value,
                StringComparer.OrdinalIgnoreCase);
            currentContracts[normalized] = ordered[0];
            var replacement = new RegistryState(
                Validate(retained).OrderBy(static item => item.DomainContractId,
                    StringComparer.Ordinal).ToArray(),
                currentContracts,
                structures);
            if (ReferenceEquals(Interlocked.CompareExchange(ref _state, replacement, current), current))
                return;
        }
    }

    public IEnumerator<DatabentoContractRegistration> GetEnumerator() =>
        ((IEnumerable<DatabentoContractRegistration>)Snapshot()).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed record RegistryState(
        DatabentoContractRegistration[] Registrations,
        IReadOnlyDictionary<string, FuturesContractV3ReadModel> OnTheRunFuturesContracts,
        IReadOnlyDictionary<string, FuturesTermStructureContracts> TermStructures);

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
    // XCBF must establish its live session before GLBX. The Databento gateway can
    // leave an XCBF subscription waiting for symbol mappings/acknowledgements when
    // it is opened after an already-running GLBX session in the same process.
    internal static int StartupPriority(string dataset) =>
        string.Equals(dataset, "XCBF.PITCH", StringComparison.Ordinal)
            ? 0
            : 1;

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
