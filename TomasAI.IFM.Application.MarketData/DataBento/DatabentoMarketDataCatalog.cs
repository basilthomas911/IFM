using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
        if (options.FeedOptions.DataSource == FeedDataSourceMode.Synthetic
            && registrations.All(static registration =>
                registration.AssetTypeId == AssetTypeId.Futures))
        {
            return CreateSyntheticFuturesCatalog(
                registrations, operationsByDataset, options);
        }
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
            var details = await QueryCatalogDefinitionsAsync(
                    operations,
                    names,
                    options,
                    cancellationToken)
                .ConfigureAwait(false);
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
                            DatabentoContractMetadata.FindCurrencyFallback(options, detail.Ticker),
                            registration.OnTheRun,
                            registration.Rollover)
                        : null,
                    registration.AssetTypeId == AssetTypeId.FuturesOption
                        ? MapConfiguredOption(
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

    private static async Task<IReadOnlyList<ContractDetail?>> QueryCatalogDefinitionsAsync(
        IDatabentoOperationRunner operations,
        string[] names,
        DatabentoMarketDataRuntimeOptions options,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var result = await operations.RunAsync(
                    queries =>
                    {
                        // This batch is the authoritative provider definition snapshot
                        // for the epoch. Native feed startup independently validates its
                        // copied symbol/instrument mappings before consumer readiness.
                        return queries.TryGetContractDetails(
                            names,
                            options.ProviderQueryTimeout);
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.IsSuccess)
            {
                return result.Details;
            }
            if (attempt >= options.CatalogQueryAttempts
                || !IsTransientCatalogFailure(result.Status))
            {
                ThrowCatalogQueryFailure(result);
            }
            var delay = TimeSpan.FromTicks(checked(
                options.CatalogQueryRetryDelay.Ticks * attempt));
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTransientCatalogFailure(DatabentoFeedStatus status) => status is
        DatabentoFeedStatus.DatabentoError
        or DatabentoFeedStatus.OsError
        or DatabentoFeedStatus.Timeout
        or DatabentoFeedStatus.ConnectionLimit
        or DatabentoFeedStatus.RateLimit
        or DatabentoFeedStatus.IncompleteDefinitions
        or DatabentoFeedStatus.ConnectionHung;

    private static void ThrowCatalogQueryFailure(
        DatabentoContractDetailsQueryResult result)
    {
        if (result.Status == DatabentoFeedStatus.Timeout)
        {
            throw new DatabentoFeedTimeoutException(
                result.ErrorMessage ?? "Catalog definition query timed out.");
        }
        throw new DatabentoFeedException(
            result.Status,
            result.ErrorMessage ?? $"Catalog definition query failed with {result.Status}.");
    }

    private static DatabentoMarketDataCatalog CreateSyntheticFuturesCatalog(
        IReadOnlyList<DatabentoContractRegistration> registrations,
        IReadOnlyDictionary<string, IDatabentoOperationRunner> operationsByDataset,
        DatabentoMarketDataRuntimeOptions options)
    {
        var resolved = registrations.Select((registration, index) =>
        {
            var contract = SyntheticFuturesContractFactory.Create(registration);
            var dataset = DatabentoDatasetSelection.Resolve(options, registration);
            var multiplier = int.TryParse(
                contract.Multiplier,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsedMultiplier)
                    ? parsedMultiplier
                    : 1;
            var detail = new ContractDetail
            {
                Dataset = dataset,
                RawSymbol = registration.ProviderContractName,
                Ticker = contract.Symbol,
                Underlying = registration.ProviderContractName,
                Instrument = new InstrumentKey(1, checked((uint)index + 1)),
                ContractKind = ContractKind.Future,
                ContractMultiplier = multiplier,
                MaturityDate = contract.LastTradeDate,
                Currency = contract.Currency,
                SettlementCurrency = contract.Currency,
                Exchange = contract.Exchange,
                SecurityType = contract.SecurityType,
                Cfi = string.Empty,
                UnitOfMeasure = contract.Currency
            };
            return new ResolvedContract(
                registration,
                dataset,
                detail,
                contract,
                null);
        }).ToArray();
        return new DatabentoMarketDataCatalog(
            resolved, operationsByDataset, options);
    }

    public FuturesContractV3ReadModel? FindFutures(string contractId) =>
        _resolved.GetValueOrDefault(contractId)?.Futures;

    public bool TryGetMarketInstrumentId(string contractId, out uint marketInstrumentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (_resolved.TryGetValue(contractId, out var resolved)
            && resolved.Detail.Instrument.InstrumentId != 0)
        {
            marketInstrumentId = resolved.Detail.Instrument.InstrumentId;
            return true;
        }
        marketInstrumentId = 0;
        return false;
    }

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

    public async Task<FuturesOptionContractReadModel[]> GetOptionChainBySymbolAsync(
        string underlyingSymbol,
        DateOnly maturityDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(underlyingSymbol);
        var underlying = _resolved.Values
            .Where(value => value.Futures is not null)
            .FirstOrDefault(value =>
                string.Equals(value.Futures!.Symbol, underlyingSymbol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.Detail.Ticker, underlyingSymbol, StringComparison.OrdinalIgnoreCase));
        if (underlying is null)
            throw new MarketDataContractNotFoundException(underlyingSymbol);
        var operations = _operationsByDataset[underlying.Dataset];
        var result = await operations.RunAsync(queries =>
        {
            var definitions = queries.GetChainDefinitions(new OptionChainDefinitionRequest
            {
                Dataset = underlying.Dataset,
                Underlying = underlyingSymbol,
                MaturityDate = maturityDate,
                UniversePolicy = OptionUniversePolicy.ParentOptionSymbol,
                Rights = OptionRightSelection.Both
            }, _options.ProviderQueryTimeout);
            if (definitions.Contracts.Count == 0)
                return Array.Empty<FuturesOptionContractReadModel>();
            var names = definitions.Contracts.Select(item => item.RawSymbol).ToArray();
            var details = queries.GetContractDetails(names, _options.ProviderQueryTimeout);
            return details.Select((detail, index) => MapOption(
                    names[index],
                    detail ?? throw new MarketDataContractMappingException(
                        names[index], "chain definition metadata could not be hydrated"),
                    underlying.Futures!.Currency))
                .ToArray();
        }).ConfigureAwait(false);
        return result
            .OrderBy(item => item.StrikePrice)
            .ThenBy(item => item.OptionType, StringComparer.Ordinal)
            .ThenBy(item => item.ContractId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<OptionContractExpiryReadModel[]> DiscoverOptionContractExpiriesAsync(
        string underlyingSymbol,
        DateOnly fromExpiry,
        DateOnly throughExpiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(underlyingSymbol);
        if (throughExpiry < fromExpiry) throw new ArgumentOutOfRangeException(nameof(throughExpiry));
        var normalized = underlyingSymbol.Trim().ToUpperInvariant();
        var underlyings = _resolved.Values
            .Where(value => value.Futures is not null
                && (string.Equals(value.Futures.Symbol, normalized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value.Detail.Ticker, normalized, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(value => value.Futures!.LastTradeDate)
            .ToArray();
        if (underlyings.Length == 0) throw new MarketDataContractNotFoundException(normalized);
        var dataset = underlyings[0].Dataset;
        var operations = _operationsByDataset[dataset];
        var rows = new List<OptionContractExpiryReadModel>();
        var rootFamilies = OptionExpiryCalendarPolicy.GetRoots(normalized);

        foreach (var (family, roots) in rootFamilies)
        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<ContractDetail> definitions;
            try
            {
                definitions = await operations.RunAsync(queries =>
                    queries.GetContractDetails($"{root}.OPT", _options.ProviderQueryTimeout)).ConfigureAwait(false);
            }
            catch (DatabentoFeedException exception) when (
                exception.Message.Contains("Could not resolve smart symbols", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var group in definitions
                         .Where(detail => detail.ContractKind is ContractKind.CallOption or ContractKind.PutOption)
                         .Where(detail => detail.MaturityDate >= fromExpiry && detail.MaturityDate <= throughExpiry)
                         .GroupBy(detail => detail.MaturityDate!.Value))
            {
                var representative = group.First();
                var contractId = ResolveUnderlyingDomainId(dataset, representative.Underlying)
                    ?? underlyings.FirstOrDefault(value => value.Futures!.LastTradeDate >= group.Key)?.Futures!.ContractId
                    ?? underlyings[^1].Futures!.ContractId;
                rows.Add(new()
                {
                    Symbol = normalized,
                    ContractId = contractId,
                    ExpiryDate = group.Key,
                    ProviderRoot = root,
                    OptionFamily = family,
                    RefreshedAtUtc = DateTime.UtcNow
                });
            }
        }

        return rows
            .DistinctBy(row => (row.ExpiryDate, row.ProviderRoot, row.ContractId))
            .OrderBy(row => row.ExpiryDate)
            .ThenBy(row => row.ProviderRoot, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<FuturesOptionContractReadModel[]> GetOptionChainByRootAsync(
        string underlyingSymbol,
        string providerRoot,
        DateOnly maturityDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(underlyingSymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerRoot);
        var underlying = _resolved.Values
            .Where(value => value.Futures is not null)
            .FirstOrDefault(value =>
                string.Equals(value.Futures!.Symbol, underlyingSymbol, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.Detail.Ticker, underlyingSymbol, StringComparison.OrdinalIgnoreCase))
            ?? throw new MarketDataContractNotFoundException(underlyingSymbol);
        var root = providerRoot.Trim().ToUpperInvariant();
        var operations = _operationsByDataset[underlying.Dataset];
        return await operations.RunAsync(queries =>
        {
            var definitions = queries.GetContractDetails(
                    $"{root}.OPT", _options.ProviderQueryTimeout)
                .Where(detail => detail.ContractKind is ContractKind.CallOption or ContractKind.PutOption)
                .Where(detail => detail.MaturityDate == maturityDate)
                .ToArray();
            return definitions.Select(detail => MapOption(
                    detail.RawSymbol, detail, underlying.Futures!.Currency))
                .OrderBy(option => option.StrikePrice)
                .ThenBy(option => option.OptionType, StringComparer.Ordinal)
                .ThenBy(option => option.ContractId, StringComparer.Ordinal)
                .ToArray();
        }).ConfigureAwait(false);
    }

    public async Task<FuturesOptionContractReadModel[]> GetOptionChainByRootAsync(
        string underlyingSymbol, string providerRoot, DateOnly fromMaturityDate,
        DateOnly throughMaturityDate, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(underlyingSymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerRoot);
        if (throughMaturityDate < fromMaturityDate) throw new ArgumentOutOfRangeException(nameof(throughMaturityDate));
        var underlying = _resolved.Values.Where(value => value.Futures is not null)
            .FirstOrDefault(value => string.Equals(value.Futures!.Symbol, underlyingSymbol, StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(value.Detail.Ticker, underlyingSymbol, StringComparison.OrdinalIgnoreCase))
            ?? throw new MarketDataContractNotFoundException(underlyingSymbol);
        var root = providerRoot.Trim().ToUpperInvariant();
        var operations = _operationsByDataset[underlying.Dataset];
        cancellationToken.ThrowIfCancellationRequested();
        return await operations.RunAsync(queries => queries.GetContractDetails($"{root}.OPT", _options.ProviderQueryTimeout)
            .Where(detail => detail.ContractKind is ContractKind.CallOption or ContractKind.PutOption)
            .Where(detail => detail.MaturityDate >= fromMaturityDate && detail.MaturityDate <= throughMaturityDate)
            .Select(detail => MapOption(detail.RawSymbol, detail, underlying.Futures!.Currency))
            .OrderBy(option => option.ContractMonth).ThenBy(option => option.StrikePrice)
            .ThenBy(option => option.OptionType, StringComparer.Ordinal).ThenBy(option => option.ContractId, StringComparer.Ordinal)
            .ToArray()).ConfigureAwait(false);
    }

    private string? ResolveUnderlyingDomainId(string dataset, string providerIdentity) =>
        _futureDomainIdByProviderIdentity.GetValueOrDefault((dataset, providerIdentity));

    private static FuturesContractV3ReadModel MapFutures(
        string domainContractId,
        ContractDetail detail,
        string? currencyFallback,
        bool onTheRun,
        bool rollover)
    {
        var maturity = detail.MaturityDate ?? ToDate(detail.ExpirationTimestampNanoseconds)
            ?? throw new MarketDataContractMappingException(
                domainContractId, "the futures maturity is missing");
        return new FuturesContractV3ReadModel(
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
            onTheRun,
            rollover);
    }

    private FuturesOptionContractReadModel MapOption(
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
        var underlyingId = ResolveUnderlyingDomainId(detail.Dataset, detail.Underlying);
        var underlying = underlyingId is null ? null : _resolved.GetValueOrDefault(underlyingId);
        var expiration = ToTimestamp(detail.ExpirationTimestampNanoseconds);
        var digestSource = string.Join("|", detail.Dataset, detail.RawSymbol,
            detail.Instrument.PublisherId, detail.Instrument.InstrumentId,
            detail.Underlying, detail.UnderlyingInstrumentId, detail.ContractKind,
            detail.StrikePrice, detail.ContractMultiplier, detail.MinimumPriceIncrement,
            detail.ExpirationTimestampNanoseconds, detail.ActivationTimestampNanoseconds);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digestSource)))
            .ToLowerInvariant();
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
            detail.ContractKind == ContractKind.CallOption ? "Call" : "Put")
        {
            StrikePriceDecimal = strikeRaw / PriceScale,
            Dataset = detail.Dataset,
            PublisherId = detail.Instrument.PublisherId,
            InstrumentId = detail.Instrument.InstrumentId,
            RawSymbol = detail.RawSymbol,
            DefinitionTimestampUtc = ToTimestamp(detail.ActivationTimestampNanoseconds),
            DefinitionDigest = digest,
            RawDefinitionReference = $"databento:{detail.Dataset}:{detail.RawSymbol}",
            ExpirationUtc = expiration,
            LastTradingUtc = expiration,
            MultiplierValue = detail.ContractMultiplier,
            PriceScale = PriceScale,
            TickSize = detail.MinimumPriceIncrement / PriceScale,
            MappingVersion = "DatabentoContractDetail/v1",
            UnderlyingContractId = underlyingId,
            UnderlyingAssetType = ReferenceAssetType.Futures,
            OptionRight = detail.ContractKind == ContractKind.CallOption
                ? ReferenceOptionRight.Call : ReferenceOptionRight.Put,
            UnderlyingInstrumentId = detail.UnderlyingInstrumentId,
            UnderlyingPublisherId = underlying?.Detail.Instrument.PublisherId
        };
    }

    private static FuturesOptionContractReadModel MapConfiguredOption(
        string domainContractId, ContractDetail detail, string? currencyFallback)
    {
        var maturity = detail.MaturityDate ?? ToDate(detail.ExpirationTimestampNanoseconds)
            ?? throw new MarketDataContractMappingException(domainContractId, "the option maturity is missing");
        var strikeRaw = detail.StrikePrice
            ?? throw new MarketDataContractMappingException(domainContractId, "the option strike is missing");
        return new FuturesOptionContractReadModel(domainContractId, detail.RawSymbol, detail.Ticker,
            detail.RawSymbol, "FOP", DatabentoContractMetadata.ResolveCurrency(detail, domainContractId,
                currencyFallback), detail.Exchange,
            (detail.ContractMultiplier ?? 1).ToString(CultureInfo.InvariantCulture), maturity,
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
        if (options.CatalogQueryAttempts <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(options), "Catalog query attempts must be positive.");
        if (options.CatalogQueryRetryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(options), "Catalog query retry delay cannot be negative.");
    }

    internal sealed record ResolvedContract(
        DatabentoContractRegistration Registration,
        string Dataset,
        ContractDetail Detail,
        FuturesContractV3ReadModel? Futures,
        FuturesOptionContractReadModel? Option);
}
