using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal sealed class DatabentoMarketDataQueries : IDatabentoMarketDataQueries
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefinitionAttemptTimeout = TimeSpan.FromSeconds(30);
    private const int DefinitionQueryAttempts = 6;
    private static readonly Regex ContractIdPattern = new(
        "^(?<symbol>[A-Z][A-Z0-9]*)(?<date>[0-9]{8})(?:(?<right>[CP])(?<strike>[0-9]+(?:\\.[0-9]+)?))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));
    private const decimal PriceScale = 1_000_000_000m;
    private readonly string _dataset;
    private readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<ContractDetail>>>
        _tickerDefinitions = new(StringComparer.Ordinal);

    internal DatabentoMarketDataQueries(string dataset)
    {
        _dataset = dataset;
    }

    public OptionChainDefinitions GetChainDefinitions(
        OptionChainDefinitionRequest request,
        TimeSpan? timeout = null)
    {
        ValidateChainRequest(request);
        var deadline = new MonotonicDeadline(timeout ?? DefaultTimeout);
        ContractDetail? selectedUnderlying = null;
        string[] roots;
        if (request.UniversePolicy == OptionUniversePolicy.UnderlyingFuture)
        {
            selectedUnderlying = GetContractDetail(
                request.Underlying,
                GetRemainingQueryTime(deadline));
            if (selectedUnderlying is not null
                && selectedUnderlying.ContractKind != ContractKind.Future)
            {
                throw new ArgumentException(
                    $"UnderlyingFuture selector '{request.Underlying}' resolved to "
                    + $"{selectedUnderlying.ContractKind}, not an outright future.",
                    nameof(request));
            }
            roots = selectedUnderlying is null ? [] : [selectedUnderlying.Ticker];
        }
        else if (request.UniversePolicy == OptionUniversePolicy.ExplicitOptionRoots)
        {
            roots = request.ExplicitOptionRoots
                .Select(NormalizeOptionRoot)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        else
        {
            roots = [NormalizeOptionRoot(request.Underlying)];
        }

        var details = new List<ContractDetail>();
        foreach (var root in roots)
        {
            details.AddRange(GetContractDetails(
                AsDefinitionParent(root, ContractKind.CallOption),
                GetRemainingQueryTime(deadline)));
        }
        return OptionChainDefinitionFilter.Create(
            _dataset,
            request,
            selectedUnderlying,
            details);
    }

    public uint ContractIdToInstrumentId(
        string contractId,
        TimeSpan? timeout = null)
    {
        var parsed = ParseContractId(contractId);
        IReadOnlyList<ContractDetail> definitions;
        try
        {
            definitions = GetContractDetails(
                AsDefinitionParent(parsed.Ticker, parsed.Kind),
                timeout);
        }
        catch (Exception exception) when (IsProviderQueryFailure(exception))
        {
            throw MappingFailure(
                ContractMappingDirection.ContractIdToInstrumentId,
                $"Contract ID '{contractId}' could not be resolved because Databento rejected or failed "
                + $"the current '{parsed.Ticker}' definition lookup in '{_dataset}'. Provider detail: "
                + exception.Message,
                contractId,
                innerException: exception);
        }
        return ResolveContractDetail(parsed, definitions, contractId).Instrument.InstrumentId;
    }

    public string InstrumentIdToContractId(
        uint instrumentId,
        TimeSpan? timeout = null)
    {
        if (instrumentId == 0)
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                "Databento instrument ID 0 is invalid; instrument IDs must be positive.",
                instrumentId: instrumentId);
        }
        ContractDetail[] matches;
        try
        {
            matches = Query(
                    [instrumentId.ToString(CultureInfo.InvariantCulture)],
                    NativeContractQueryKind.InstrumentId,
                    timeout)
                .Where(static detail => detail is not null)
                .Select(static detail => detail!)
                .ToArray();
        }
        catch (Exception exception) when (IsProviderQueryFailure(exception))
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Instrument ID {instrumentId} could not be resolved because Databento rejected or failed "
                + $"the current instrument-ID definition lookup in '{_dataset}'. Provider detail: "
                + exception.Message,
                instrumentId: instrumentId,
                innerException: exception);
        }
        if (matches.Length == 0)
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Databento instrument ID {instrumentId} is not present in the latest available "
                + $"'{_dataset}' definition interval. Instrument IDs may be remapped between trading days.",
                instrumentId: instrumentId);
        }
        if (matches.Length != 1)
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Databento instrument ID {instrumentId} resolved to {matches.Length} current definitions "
                + $"in '{_dataset}' ({Describe(matches)}); the mapping is ambiguous.",
                instrumentId: instrumentId);
        }
        var definition = matches[0];
        if (definition.Instrument.InstrumentId != instrumentId)
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Databento returned instrument ID {definition.Instrument.InstrumentId} while resolving "
                + $"requested instrument ID {instrumentId}; the provider mapping was rejected.",
                instrumentId: instrumentId);
        }
        var contractId = FormatContractId(definition, instrumentId);
        var parsed = ParseContractId(contractId);
        IReadOnlyList<ContractDetail> currentDefinitions;
        try
        {
            currentDefinitions = GetContractDetails(
                AsDefinitionParent(parsed.Ticker, parsed.Kind),
                timeout);
        }
        catch (Exception exception) when (IsProviderQueryFailure(exception))
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Instrument ID {instrumentId} resolved to contract ID '{contractId}', but Databento "
                + $"rejected or failed its current round-trip definition lookup. Provider detail: "
                + exception.Message,
                contractId,
                instrumentId,
                exception);
        }
        ContractDetail roundTrip;
        try
        {
            roundTrip = ResolveContractDetail(parsed, currentDefinitions, contractId);
        }
        catch (DatabentoContractMappingException exception)
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Instrument ID {instrumentId} resolved to contract ID '{contractId}', but that ID did "
                + $"not produce a unique current round-trip mapping. Detail: {exception.Message}",
                contractId,
                instrumentId,
                exception);
        }
        if (roundTrip.Instrument.InstrumentId != instrumentId)
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Instrument ID {instrumentId} formats as contract ID '{contractId}', but that contract ID "
                + $"currently maps to instrument ID {roundTrip.Instrument.InstrumentId}; the mapping is not bijective.",
                contractId,
                instrumentId);
        }
        return contractId;
    }

    public ContractDetail? GetContractDetail(
        string contractName,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractName);
        return Query([contractName], NativeContractQueryKind.Exact, timeout)[0];
    }

    public IReadOnlyList<ContractDetail> GetContractDetails(
        string ticker,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);
        var normalizedTicker = ticker.Trim().ToUpperInvariant();
        var cached = _tickerDefinitions.GetOrAdd(
            normalizedTicker,
            _ => new(
                () => QueryTickerWithRetries(normalizedTicker, timeout),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return cached.Value;
        }
        catch
        {
            _tickerDefinitions.TryRemove(normalizedTicker, out _);
            throw;
        }
    }

    private IReadOnlyList<ContractDetail> QueryTickerWithRetries(
        string ticker,
        TimeSpan? timeout)
    {
        var deadline = new MonotonicDeadline(timeout ?? DefaultTimeout);
        for (var attempt = 1; ; attempt++)
        {
            var remaining = deadline.Remaining;
            if (remaining <= TimeSpan.Zero)
            {
                throw new DatabentoFeedTimeoutException(
                    $"Definition lookup for '{ticker}' exceeded its timeout.");
            }
            var attemptTimeout = remaining < DefinitionAttemptTimeout
                ? remaining
                : DefinitionAttemptTimeout;
            var result = TryQuery(
                [ticker],
                NativeContractQueryKind.Ticker,
                attemptTimeout);
            if (result.IsSuccess)
            {
                return result.Details
                    .Select(static detail => detail!)
                    .ToArray();
            }
            if (attempt >= DefinitionQueryAttempts
                || deadline.Remaining <= TimeSpan.Zero)
            {
                ThrowQueryFailure(result);
            }
            // Retry within the caller's original deadline. Definition range
            // requests occasionally receive transient provider 504s.
        }
    }

    public IReadOnlyList<ContractDetail?> GetContractDetails(
        string[] contractNames,
        TimeSpan? timeout = null)
    {
        var result = TryGetContractDetails(contractNames, timeout);
        if (!result.IsSuccess)
        {
            ThrowQueryFailure(result);
        }
        return result.Details;
    }

    public DatabentoContractDetailsQueryResult TryGetContractDetails(
        string[] contractNames,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(contractNames);
        if (contractNames.Length == 0)
        {
            return DatabentoContractDetailsQueryResult.Success([]);
        }
        foreach (var contractName in contractNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contractName);
        }
        return TryQuery(contractNames, NativeContractQueryKind.Exact, timeout);
    }

    private unsafe ContractDetail?[] Query(
        IReadOnlyList<string> symbols,
        NativeContractQueryKind queryKind,
        TimeSpan? requestedTimeout)
    {
        var result = TryQuery(symbols, queryKind, requestedTimeout);
        if (!result.IsSuccess)
        {
            ThrowQueryFailure(result);
        }
        return result.Details as ContractDetail?[] ?? result.Details.ToArray();
    }

    private unsafe DatabentoContractDetailsQueryResult TryQuery(
        IReadOnlyList<string> symbols,
        NativeContractQueryKind queryKind,
        TimeSpan? requestedTimeout)
    {
        var timeout = ToTimeoutMilliseconds(requestedTimeout ?? DefaultTimeout);
        var datasetBytes = Encoding.UTF8.GetBytes(_dataset);
        var symbolBytes = new byte[symbols.Count][];
        var totalBytes = datasetBytes.Length;
        for (var index = 0; index < symbols.Count; ++index)
        {
            symbolBytes[index] = Encoding.UTF8.GetBytes(symbols[index]);
            totalBytes = checked(totalBytes + symbolBytes[index].Length);
        }
        var blob = new byte[totalBytes];
        datasetBytes.CopyTo(blob, 0);
        var slices = new NativeUtf8Slice[symbols.Count];
        var offset = datasetBytes.Length;
        for (var index = 0; index < symbolBytes.Length; ++index)
        {
            symbolBytes[index].CopyTo(blob, offset);
            slices[index] = new NativeUtf8Slice
            {
                Offset = checked((uint)offset),
                Length = checked((uint)symbolBytes[index].Length)
            };
            offset = checked(offset + symbolBytes[index].Length);
        }
        var query = new NativeContractQuery
        {
            StructSize = (uint)Unsafe.SizeOf<NativeContractQuery>(),
            AbiVersion = NativeConstants.AbiVersion,
            QueryKind = queryKind,
            TimeoutMilliseconds = timeout,
            DatasetOffset = 0,
            DatasetLength = checked((uint)datasetBytes.Length),
            SymbolCount = checked((uint)symbols.Count)
        };

        nint nativeResult;
        DatabentoFeedStatus status;
        fixed (NativeUtf8Slice* slicesPointer = slices)
        fixed (byte* blobPointer = blob)
        {
            status = NativeMethods.ContractDetailsQuery(
                &query,
                slicesPointer,
                blobPointer,
                checked((uint)blob.Length),
                out nativeResult);
        }
        using var result = nativeResult == 0
            ? null
            : new SafeContractDetailsResultHandle(nativeResult);
        if (status != DatabentoFeedStatus.Ok)
        {
            return DatabentoContractDetailsQueryResult.Failure(
                status,
                FormatQueryFailure(status, ReadQueryError(result)));
        }

        status = NativeMethods.ContractDetailsResultGetCounts(
            result!, out var detailCount, out var utf8Bytes);
        if (status != DatabentoFeedStatus.Ok)
        {
            return DatabentoContractDetailsQueryResult.Failure(
                status,
                $"Read contract-detail result counts failed with {status}.");
        }
        var nativeDetails = new NativeContractDetail[checked((int)detailCount)];
        var resultBlob = new byte[checked((int)utf8Bytes)];
        fixed (NativeContractDetail* detailsPointer = nativeDetails)
        fixed (byte* resultBlobPointer = resultBlob)
        {
            status = NativeMethods.ContractDetailsResultCopy(
                result!, detailsPointer, detailCount,
                resultBlobPointer, utf8Bytes);
        }
        if (status != DatabentoFeedStatus.Ok)
        {
            return DatabentoContractDetailsQueryResult.Failure(
                status,
                $"Copy contract-detail results failed with {status}.");
        }
        var managed = new ContractDetail?[nativeDetails.Length];
        for (var index = 0; index < nativeDetails.Length; ++index)
        {
            managed[index] = Convert(nativeDetails[index], resultBlob);
        }
        return DatabentoContractDetailsQueryResult.Success(managed);
    }

    private ContractDetail? Convert(NativeContractDetail source, byte[] utf8Blob)
    {
        if ((source.Flags & NativeContractDetailFlags.Found) == 0)
        {
            return null;
        }
        return new ContractDetail
        {
            Dataset = _dataset,
            RawSymbol = Decode(utf8Blob, source.RawSymbol),
            Ticker = Decode(utf8Blob, source.Asset),
            Underlying = Decode(utf8Blob, source.Underlying),
            Instrument = new InstrumentKey(source.PublisherId, source.InstrumentId),
            ContractKind = (ContractKind)source.ContractKind,
            RawInstrumentId = source.RawInstrumentId,
            UnderlyingInstrumentId = source.UnderlyingId,
            ContractMultiplier = Has(source, NativeContractDetailFlags.HasMultiplier)
                ? source.ContractMultiplier
                : null,
            StrikePrice = Has(source, NativeContractDetailFlags.HasStrikePrice)
                ? source.StrikePrice
                : null,
            MinimumPriceIncrement = Has(
                    source, NativeContractDetailFlags.HasMinimumPriceIncrement)
                ? source.MinimumPriceIncrement
                : null,
            MinimumPriceIncrementAmount = Has(
                    source, NativeContractDetailFlags.HasMinimumPriceIncrementAmount)
                ? source.MinimumPriceIncrementAmount
                : null,
            ExpirationTimestampNanoseconds = Has(
                    source, NativeContractDetailFlags.HasExpiration)
                ? source.ExpirationTimestampNanoseconds
                : null,
            ActivationTimestampNanoseconds = Has(
                    source, NativeContractDetailFlags.HasActivation)
                ? source.ActivationTimestampNanoseconds
                : null,
            MaturityDate = GetMaturityDate(source),
            MaturityWeek = Has(source, NativeContractDetailFlags.HasMaturityWeek)
                ? source.MaturityWeek
                : null,
            Currency = Decode(utf8Blob, source.Currency),
            SettlementCurrency = Decode(utf8Blob, source.SettlementCurrency),
            Exchange = Decode(utf8Blob, source.Exchange),
            SecurityType = Decode(utf8Blob, source.SecurityType),
            Cfi = Decode(utf8Blob, source.Cfi),
            UnitOfMeasure = Decode(utf8Blob, source.UnitOfMeasure)
        };
    }

    private static bool Has(
        NativeContractDetail source,
        NativeContractDetailFlags flag) => (source.Flags & flag) != 0;

    private void ValidateChainRequest(OptionChainDefinitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Underlying);
        if (!string.Equals(request.Dataset, _dataset, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Definition request dataset '{request.Dataset}' does not match the query "
                + $"service dataset '{_dataset}'.",
                nameof(request));
        }
        if (request.MaturityDate == DateOnly.MinValue)
        {
            throw new ArgumentException("An exact option maturity date is required.", nameof(request));
        }
        if (request.UniversePolicy is not (
                OptionUniversePolicy.ParentOptionSymbol
                or OptionUniversePolicy.UnderlyingFuture
                or OptionUniversePolicy.ExplicitOptionRoots))
        {
            throw new ArgumentException("The option universe policy is invalid.", nameof(request));
        }
        if (request.Rights == OptionRightSelection.None
            || (request.Rights & ~OptionRightSelection.Both) != 0)
        {
            throw new ArgumentException("Select Call, Put, or Both option rights.", nameof(request));
        }
        ArgumentNullException.ThrowIfNull(request.ExplicitOptionRoots);
        if (request.UniversePolicy == OptionUniversePolicy.ExplicitOptionRoots)
        {
            if (request.ExplicitOptionRoots.Count == 0)
            {
                throw new ArgumentException(
                    "ExplicitOptionRoots requires at least one option root.",
                    nameof(request));
            }
            foreach (var root in request.ExplicitOptionRoots)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(root);
            }
        }
        else if (request.ExplicitOptionRoots.Count != 0)
        {
            throw new ArgumentException(
                "Explicit option roots are only valid with ExplicitOptionRoots policy.",
                nameof(request));
        }
    }

    private static string NormalizeOptionRoot(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var root = value.Trim();
        if (root.EndsWith(".OPT", StringComparison.Ordinal))
        {
            root = root[..^4];
        }
        return root;
    }

    private static TimeSpan GetRemainingQueryTime(MonotonicDeadline deadline)
    {
        var remaining = deadline.Remaining;
        if (remaining <= TimeSpan.Zero)
        {
            throw new DatabentoFeedTimeoutException(
                "Option-chain definition discovery exceeded its timeout.");
        }
        return remaining;
    }

    private static DateOnly? GetMaturityDate(NativeContractDetail source)
    {
        if (Has(source, NativeContractDetailFlags.HasMaturityDate))
        {
            try
            {
                return new DateOnly(
                    source.MaturityYear,
                    source.MaturityMonth,
                    source.MaturityDay);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new DatabentoFeedException(
                    DatabentoFeedStatus.AbiMismatch,
                    "A native contract detail marked an invalid maturity date as present.");
            }
        }
        return Has(source, NativeContractDetailFlags.HasExpiration)
               && TryGetExpirationDate(source.ExpirationTimestampNanoseconds, out var expiration)
            ? expiration
            : null;
    }

    private static bool TryGetExpirationDate(
        ulong nanoseconds,
        out DateOnly expiration)
    {
        expiration = default;
        if (nanoseconds / 1_000_000_000UL > long.MaxValue)
        {
            return false;
        }
        try
        {
            var timestamp = DateTimeOffset.FromUnixTimeSeconds(
                checked((long)(nanoseconds / 1_000_000_000UL)));
            expiration = DateOnly.FromDateTime(timestamp.UtcDateTime);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string Decode(byte[] blob, NativeUtf8Slice slice)
    {
        var offset = checked((int)slice.Offset);
        var length = checked((int)slice.Length);
        if (offset < 0 || length < 0 || offset > blob.Length - length)
        {
            throw new DatabentoFeedException(
                DatabentoFeedStatus.AbiMismatch,
                "A native contract-detail string was outside the returned UTF-8 buffer.");
        }
        return Encoding.UTF8.GetString(blob, offset, length);
    }

    private static uint ToTimeoutMilliseconds(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds >= uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "The contract-detail timeout must be positive and less than uint.MaxValue milliseconds.");
        }
        return checked((uint)Math.Ceiling(timeout.TotalMilliseconds));
    }

    private ContractDetail ResolveContractDetail(
        ParsedContractId parsed,
        IReadOnlyList<ContractDetail> definitions,
        string originalContractId)
    {
        var matches = definitions.Where(definition => Matches(parsed, definition)).ToArray();
        if (matches.Length == 0)
        {
            var related = definitions
                .Where(definition => definition.ContractKind == parsed.Kind)
                .Take(5)
                .ToArray();
            var suffix = related.Length == 0
                ? "No current definitions of the requested contract type were returned."
                : $"Example current definitions: {Describe(related)}.";
            throw MappingFailure(
                ContractMappingDirection.ContractIdToInstrumentId,
                $"Contract ID '{originalContractId}' parsed as ticker '{parsed.Ticker}', expiration "
                + $"{parsed.Expiration:yyyy-MM-dd}, type {parsed.Kind}"
                + (parsed.StrikePrice is null ? ". " : $", strike {parsed.StrikePrice}. ")
                + $"No exact current definition was found in '{_dataset}'. {suffix}",
                originalContractId);
        }
        if (matches.Length != 1)
        {
            throw MappingFailure(
                ContractMappingDirection.ContractIdToInstrumentId,
                $"Contract ID '{originalContractId}' matched {matches.Length} current definitions in "
                + $"'{_dataset}' ({Describe(matches)}); the mapping is ambiguous.",
                originalContractId);
        }
        if (matches[0].Instrument.InstrumentId == 0)
        {
            throw MappingFailure(
                ContractMappingDirection.ContractIdToInstrumentId,
                $"Contract ID '{originalContractId}' resolved to provider instrument ID 0, which is invalid.",
                originalContractId);
        }
        return matches[0];
    }

    private static bool Matches(ParsedContractId parsed, ContractDetail definition)
    {
        if (!string.Equals(parsed.Ticker, definition.Ticker, StringComparison.Ordinal)
            || parsed.Kind != definition.ContractKind
            || !TryGetExpirationDate(definition, out var expiration)
            || expiration != parsed.Expiration)
        {
            return false;
        }
        return parsed.Kind == ContractKind.Future
            || definition.StrikePrice == parsed.StrikePrice;
    }

    private static ParsedContractId ParseContractId(string contractId)
    {
        if (string.IsNullOrWhiteSpace(contractId))
        {
            throw MappingFailure(
                ContractMappingDirection.ContractIdToInstrumentId,
                "Contract ID is required and cannot be empty.",
                contractId);
        }
        var match = ContractIdPattern.Match(contractId);
        if (!match.Success)
        {
            throw MappingFailure(
                ContractMappingDirection.ContractIdToInstrumentId,
                $"Contract ID '{contractId}' is invalid. Expected FUTURE as SYMBOLyyyyMMdd "
                + "(for example ES20260918), or FUTURES OPTION as SYMBOLyyyyMMddCstrike/"
                + "SYMBOLyyyyMMddPstrike (for example ES20260918C6950 or ES20260918P6950.5).",
                contractId);
        }
        if (!DateOnly.TryParseExact(
                match.Groups["date"].Value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var expiration))
        {
            throw MappingFailure(
                ContractMappingDirection.ContractIdToInstrumentId,
                $"Contract ID '{contractId}' contains an invalid yyyyMMdd expiration date "
                + $"'{match.Groups["date"].Value}'.",
                contractId);
        }
        var right = match.Groups["right"].Value;
        if (right.Length == 0)
        {
            return new ParsedContractId(
                match.Groups["symbol"].Value,
                expiration,
                ContractKind.Future,
                null);
        }
        var strikeText = match.Groups["strike"].Value;
        if (!decimal.TryParse(
                strikeText,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var strike)
            || strike <= 0
            || strike > long.MaxValue / PriceScale
            || decimal.Truncate(strike * PriceScale) != strike * PriceScale)
        {
            throw MappingFailure(
                ContractMappingDirection.ContractIdToInstrumentId,
                $"Contract ID '{contractId}' contains strike '{strikeText}', which must be positive and "
                + "exactly representable with at most nine fractional decimal places.",
                contractId);
        }
        return new ParsedContractId(
            match.Groups["symbol"].Value,
            expiration,
            right == "C" ? ContractKind.CallOption : ContractKind.PutOption,
            decimal.ToInt64(strike * PriceScale));
    }

    private string FormatContractId(ContractDetail definition, uint instrumentId)
    {
        if (definition.ContractKind is not (
                ContractKind.Future or ContractKind.CallOption or ContractKind.PutOption))
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Instrument ID {instrumentId} is {definition.ContractKind}; only outright futures and "
                + "futures options can be converted to application contract IDs.",
                instrumentId: instrumentId);
        }
        if (string.IsNullOrWhiteSpace(definition.Ticker))
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Instrument ID {instrumentId} has no Databento definition asset/ticker.",
                instrumentId: instrumentId);
        }
        if (!TryGetExpirationDate(definition, out var expiration))
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Instrument ID {instrumentId} has no valid Databento expiration timestamp.",
                instrumentId: instrumentId);
        }
        var prefix = definition.Ticker + expiration.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        if (definition.ContractKind == ContractKind.Future)
        {
            return prefix;
        }
        if (definition.StrikePrice is null || definition.StrikePrice <= 0)
        {
            throw MappingFailure(
                ContractMappingDirection.InstrumentIdToContractId,
                $"Futures option instrument ID {instrumentId} has no positive Databento strike price.",
                instrumentId: instrumentId);
        }
        var strike = (definition.StrikePrice.Value / PriceScale)
            .ToString("0.#########", CultureInfo.InvariantCulture);
        var right = definition.ContractKind == ContractKind.CallOption ? "C" : "P";
        return prefix + right + strike;
    }

    private static bool TryGetExpirationDate(
        ContractDetail definition,
        out DateOnly expiration)
    {
        expiration = default;
        return definition.ExpirationTimestampNanoseconds is { } nanoseconds
               && TryGetExpirationDate(nanoseconds, out expiration);
    }

    private static string Describe(IEnumerable<ContractDetail> definitions) =>
        string.Join(
            ", ",
            definitions.Select(definition =>
                $"{definition.RawSymbol}/instrument={definition.Instrument.InstrumentId}"));

    private static string AsDefinitionParent(
        string ticker,
        ContractKind kind)
    {
        var normalized = ticker.Trim().ToUpperInvariant();
        if (normalized.EndsWith(".FUT", StringComparison.Ordinal)
            || normalized.EndsWith(".OPT", StringComparison.Ordinal))
        {
            return normalized;
        }
        return kind == ContractKind.Future
            ? $"{normalized}.FUT"
            : $"{normalized}.OPT";
    }

    private static bool IsProviderQueryFailure(Exception exception) =>
        (exception is DatabentoFeedException or DatabentoFeedTimeoutException)
        && exception is not DatabentoContractMappingException;

    private static DatabentoContractMappingException MappingFailure(
        ContractMappingDirection direction,
        string message,
        string? contractId = null,
        uint? instrumentId = null,
        Exception? innerException = null) =>
        new(direction, message, contractId, instrumentId, innerException);

    private sealed record ParsedContractId(
        string Ticker,
        DateOnly Expiration,
        ContractKind Kind,
        long? StrikePrice);

    private static void ThrowQueryFailure(
        DatabentoContractDetailsQueryResult result)
    {
        if (result.Status == DatabentoFeedStatus.Timeout)
        {
            throw new DatabentoFeedTimeoutException(
                result.ErrorMessage ?? "Query contract details timed out.");
        }
        throw new DatabentoFeedException(
            result.Status,
            result.ErrorMessage ?? $"Query contract details failed with {result.Status}.");
    }

    private static unsafe string? ReadQueryError(
        SafeContractDetailsResultHandle? result)
    {
        string? detail = null;
        if (result is not null && !result.IsInvalid && !result.IsClosed)
        {
            var readStatus = NativeMethods.ContractDetailsResultGetError(
                result, null, 0, out var required);
            if (readStatus == DatabentoFeedStatus.BufferTooSmall && required > 1)
            {
                var buffer = new byte[required];
                fixed (byte* pointer = buffer)
                {
                    readStatus = NativeMethods.ContractDetailsResultGetError(
                        result, pointer, required, out _);
                }
                if (readStatus == DatabentoFeedStatus.Ok)
                {
                    detail = Encoding.UTF8.GetString(
                        buffer.AsSpan(0, checked((int)required - 1)));
                }
            }
        }
        return detail;
    }

    private static string FormatQueryFailure(
        DatabentoFeedStatus status,
        string? detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? $"Query contract details failed with {status}."
            : $"Query contract details failed with {status}: {detail}";
}
