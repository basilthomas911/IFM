using System.Collections.Frozen;
using System.Globalization;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento;

internal sealed class DatabentoMarketDataCatalog : IDatabentoMarketDataCatalog
{
    private const decimal PriceScale = 1_000_000_000m;
    private readonly FrozenDictionary<string, ResolvedContract> _resolved;
    private readonly FrozenDictionary<(string Dataset, string ProviderIdentity), string>
        _futureDomainIdByProviderIdentity;
    private readonly FrozenDictionary<string, IDatabentoOperationRunner> _operationsByDataset;
    private readonly DatabentoMarketDataRuntimeOptions _options;

    private DatabentoMarketDataCatalog(
        IEnumerable<ResolvedContract> resolved,
        IReadOnlyDictionary<string, IDatabentoOperationRunner> operationsByDataset,
        DatabentoMarketDataRuntimeOptions options)
    {
        _resolved = resolved.ToFrozenDictionary(
            item => item.Registration.DomainContractId,
            StringComparer.Ordinal);
        _futureDomainIdByProviderIdentity = resolved
            .Where(item => item.Registration.AssetTypeId == AssetTypeId.Futures)
            .SelectMany(item => new[]
            {
                new KeyValuePair<(string, string), string>(
                    (item.Dataset, item.Registration.ProviderContractName),
                    item.Registration.DomainContractId),
                new KeyValuePair<(string, string), string>(
                    (item.Dataset, item.Detail.RawSymbol),
                    item.Registration.DomainContractId)
            })
            .GroupBy(item => item.Key)
            .ToFrozenDictionary(
                group => group.Key,
                group => group.First().Value);
        _operationsByDataset = operationsByDataset.ToFrozenDictionary(StringComparer.Ordinal);
        _options = options;
    }

    internal IReadOnlyCollection<ResolvedContract> ResolvedContracts => _resolved.Values;

    internal static async Task<DatabentoMarketDataCatalog> CreateAsync(
        IReadOnlyDictionary<string, IDatabentoOperationRunner> operationsByDataset,
        DatabentoMarketDataRuntimeOptions options,
        CancellationToken cancellationToken)
    {
        ValidateOptions(options);
        var registrations = options.Contracts.ToArray();
        var resolved = new ResolvedContract[registrations.Length];
        var indexed = registrations.Select(static (registration, index) =>
            (Registration: registration, Index: index));
        var groups = indexed.GroupBy(item =>
                DatabentoDatasetSelection.Resolve(options, item.Registration),
                StringComparer.Ordinal)
            .Select(group => (Dataset: group.Key, Entries: group.ToArray()))
            .ToArray();
        var groupTasks = groups.Select(async group =>
        {
            if (!operationsByDataset.TryGetValue(group.Dataset, out var operations))
                throw new InvalidOperationException(
                    $"No DataBento operation runner is configured for dataset '{group.Dataset}'.");
            var entries = group.Entries;
            var names = entries.Select(item => item.Registration.ProviderContractName).ToArray();
            var details = await operations.RunAsync(
                queries =>
                {
                    var batch = queries.GetContractDetails(names, options.ProviderQueryTimeout);
                    for (var index = 0; index < batch.Count; index++)
                    {
                        var detail = batch[index];
                        if (detail is null) continue;
                        var reverse = queries.InstrumentIdToContractId(
                            detail.Instrument.InstrumentId, options.ProviderQueryTimeout);
                        var forward = queries.ContractIdToInstrumentId(
                            reverse, options.ProviderQueryTimeout);
                        if (forward != detail.Instrument.InstrumentId)
                            throw new MarketDataContractMappingException(
                                entries[index].Registration.DomainContractId,
                                "the forward instrument mapping did not match contract metadata");
                    }
                    return batch;
                },
                cancellationToken).ConfigureAwait(false);
            if (details.Count != entries.Length)
                throw new MarketDataContractMappingException(
                    "epoch", $"the provider batch result count did not match the request for dataset '{group.Dataset}'");

            var groupResults = new (int Index, ResolvedContract Contract)[entries.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                var registration = entry.Registration;
                var detail = details[index] ?? throw new MarketDataContractNotFoundException(
                    registration.DomainContractId);
                ValidateKind(registration, detail);
                groupResults[index] = (entry.Index, new ResolvedContract(
                    registration,
                    group.Dataset,
                    detail,
                    registration.AssetTypeId == AssetTypeId.Futures
                        ? MapFutures(
                            registration.DomainContractId,
                            detail,
                            DatabentoContractMetadata.FindCurrencyFallback(options, detail.Ticker))
                        : null,
                    registration.AssetTypeId == AssetTypeId.FuturesOption
                        ? MapOption(
                            registration.DomainContractId,
                            detail,
                            DatabentoContractMetadata.FindCurrencyFallback(options, detail.Ticker))
                        : null));
            }
            return groupResults;
        }).ToArray();

        var resolvedGroups = await Task.WhenAll(groupTasks).ConfigureAwait(false);
        foreach (var group in resolvedGroups)
        foreach (var item in group)
            resolved[item.Index] = item.Contract;

        return new DatabentoMarketDataCatalog(resolved, operationsByDataset, options);
    }

    public FuturesContractV2ReadModel? FindFutures(string contractId) =>
        _resolved.GetValueOrDefault(contractId)?.Futures;

    public FuturesOptionContractReadModel? FindFuturesOption(string contractId) =>
        _resolved.GetValueOrDefault(contractId)?.Option;

    public string? FindOptionUnderlying(string futuresOptionContractId)
    {
        if (!_resolved.TryGetValue(futuresOptionContractId, out var option)
            || option.Option is null)
            return null;
        return ResolveUnderlyingDomainId(option.Dataset, option.Detail.Underlying);
    }

    public async Task<FuturesOptionContractReadModel[]> GetOptionChainAsync(
        string futuresContractId,
        DateOnly maturityDate)
    {
        if (!_resolved.TryGetValue(futuresContractId, out var underlying)
            || underlying.Futures is null)
            throw new MarketDataContractNotFoundException(futuresContractId);

        var operations = _operationsByDataset[underlying.Dataset];
        var result = await operations.RunAsync(queries =>
        {
            var definitions = queries.GetChainDefinitions(
                new OptionChainDefinitionRequest
                {
                    Dataset = underlying.Dataset,
                    Underlying = underlying.Detail.RawSymbol,
                    MaturityDate = maturityDate,
                    UniversePolicy = OptionUniversePolicy.UnderlyingFuture,
                    Rights = OptionRightSelection.Both
                },
                _options.ProviderQueryTimeout);
            if (definitions.Contracts.Count == 0)
                return Array.Empty<FuturesOptionContractReadModel>();

            var names = definitions.Contracts.Select(item => item.RawSymbol).ToArray();
            var details = queries.GetContractDetails(names, _options.ProviderQueryTimeout);
            var mapped = new FuturesOptionContractReadModel[definitions.Contracts.Count];
            for (var index = 0; index < definitions.Contracts.Count; index++)
            {
                var detail = details[index] ?? throw new MarketDataContractMappingException(
                    names[index], "chain definition metadata could not be hydrated");
                mapped[index] = MapOption(
                    names[index],
                    detail,
                    underlying.Futures.Currency);
            }
            return mapped;
        }).ConfigureAwait(false);

        return result
            .OrderBy(item => item.StrikePrice)
            .ThenBy(item => item.OptionType, StringComparer.Ordinal)
            .ThenBy(item => item.ContractId, StringComparer.Ordinal)
            .ToArray();
    }

    private string? ResolveUnderlyingDomainId(string dataset, string providerIdentity) =>
        _futureDomainIdByProviderIdentity.GetValueOrDefault((dataset, providerIdentity));

    private static FuturesContractV2ReadModel MapFutures(
        string domainContractId,
        ContractDetail detail,
        string? currencyFallback)
    {
        var maturity = detail.MaturityDate ?? ToDate(detail.ExpirationTimestampNanoseconds)
            ?? throw new MarketDataContractMappingException(
                domainContractId, "the futures maturity is missing");
        return new FuturesContractV2ReadModel(
            domainContractId,
            detail.RawSymbol,
            detail.Ticker,
            detail.RawSymbol,
            "FUT",
            DatabentoContractMetadata.ResolveCurrency(
                detail,
                domainContractId,
                currencyFallback),
            detail.Exchange,
            (detail.ContractMultiplier ?? 1).ToString(CultureInfo.InvariantCulture),
            maturity,
            IsCurrentlyTraded(detail));
    }

    private static FuturesOptionContractReadModel MapOption(
        string domainContractId,
        ContractDetail detail,
        string? currencyFallback)
    {
        var maturity = detail.MaturityDate ?? ToDate(detail.ExpirationTimestampNanoseconds)
            ?? throw new MarketDataContractMappingException(
                domainContractId, "the option maturity is missing");
        var strikeRaw = detail.StrikePrice
            ?? throw new MarketDataContractMappingException(
                domainContractId, "the option strike is missing");
        return new FuturesOptionContractReadModel(
            domainContractId,
            detail.RawSymbol,
            detail.Ticker,
            detail.RawSymbol,
            "FOP",
            DatabentoContractMetadata.ResolveCurrency(
                detail,
                domainContractId,
                currencyFallback),
            detail.Exchange,
            (detail.ContractMultiplier ?? 1).ToString(CultureInfo.InvariantCulture),
            maturity,
            MapStrike(domainContractId, strikeRaw),
            detail.ContractKind == ContractKind.CallOption ? "Call" : "Put");
    }

    private static double MapStrike(string domainContractId, long strikeRaw)
    {
        var exact = strikeRaw / PriceScale;
        var mapped = decimal.ToDouble(exact);
        if (!double.IsFinite(mapped) || (decimal)mapped != exact)
            throw new MarketDataContractMappingException(
                domainContractId,
                $"strike {exact} cannot be represented by the existing double domain property without loss");
        return mapped;
    }

    private static bool IsCurrentlyTraded(ContractDetail detail)
    {
        var now = DateTimeOffset.UtcNow;
        var activation = ToTimestamp(detail.ActivationTimestampNanoseconds);
        var expiration = ToTimestamp(detail.ExpirationTimestampNanoseconds);
        return (activation is null || activation <= now)
            && (expiration is null || expiration >= now);
    }

    private static DateOnly? ToDate(ulong? nanoseconds) =>
        ToTimestamp(nanoseconds) is { } timestamp
            ? DateOnly.FromDateTime(timestamp.UtcDateTime)
            : null;

    private static DateTimeOffset? ToTimestamp(ulong? nanoseconds)
    {
        if (nanoseconds is null || nanoseconds > long.MaxValue) return null;
        try { return DateTimeOffset.UnixEpoch.AddTicks((long)nanoseconds.Value / 100L); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static void ValidateKind(
        DatabentoContractRegistration registration,
        ContractDetail detail)
    {
        var valid = registration.AssetTypeId switch
        {
            AssetTypeId.Futures => detail.ContractKind == ContractKind.Future,
            AssetTypeId.FuturesOption => detail.ContractKind is
                ContractKind.CallOption or ContractKind.PutOption,
            _ => false
        };
        if (!valid)
            throw new MarketDataContractKindMismatchException(
                registration.DomainContractId,
                registration.AssetTypeId.ToString(),
                detail.ContractKind.ToString());
    }

    private static void ValidateOptions(DatabentoMarketDataRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.FeedOptions);
        ArgumentNullException.ThrowIfNull(options.Contracts);
        if (options.Contracts.Count == 0)
            throw new ArgumentException("At least one configured contract is required.", nameof(options));
        if (options.Contracts.Select(item => item.DomainContractId)
            .Distinct(StringComparer.Ordinal).Count() != options.Contracts.Count)
            throw new ArgumentException("Domain contract IDs must be unique.", nameof(options));
    }

    internal sealed record ResolvedContract(
        DatabentoContractRegistration Registration,
        string Dataset,
        ContractDetail Detail,
        FuturesContractV2ReadModel? Futures,
        FuturesOptionContractReadModel? Option);
}
