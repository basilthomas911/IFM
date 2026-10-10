using System.Globalization;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.Api.Server.Core.MarketData.InstrumentDefinitions;

internal static class StoredOptionDefinitionRangeLoader
{
    const int PageSize = 200;

    internal static async Task<FuturesContractV3ReadModel[]> LoadFuturesAsync(
        IInstrumentDefinitionStore store,
        string symbol,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        symbol = symbol.Trim().ToUpperInvariant();
        var snapshot = await store.GetSnapshotAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Instrument definitions have not been loaded. Run the instrument-definition refresh.");
        var futures = new List<FuturesContractV3ReadModel>();
        foreach (var dataset in snapshot.Datasets
                     .Where(static dataset => !string.IsNullOrWhiteSpace(dataset))
                     .Distinct(StringComparer.Ordinal))
        {
            var definitions = await LoadSelectionsAsync(
                store, snapshot, dataset, symbol, false, at, cancellationToken).ConfigureAwait(false);
            futures.AddRange(definitions
                .Where(static definition => definition.InstrumentClass == "F"
                                            && definition.ExpirationUtc is not null)
                .Select(definition => MapToFuture(symbol, definition)));
        }

        return futures
            .DistinctBy(static future => (future.Dataset, future.PublisherId, future.InstrumentId))
            .OrderBy(static future => future.LastTradeDate)
            .ThenBy(static future => future.ContractId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static async Task<FuturesOptionContractReadModel[]> LoadAsync(
        IInstrumentDefinitionStore store,
        string symbol,
        string providerRoot,
        DateOnly fromMaturityDate,
        DateOnly throughMaturityDate,
        IReadOnlyList<FuturesContractV3ReadModel> futures,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerRoot);
        ArgumentNullException.ThrowIfNull(futures);
        if (throughMaturityDate < fromMaturityDate)
            throw new ArgumentOutOfRangeException(nameof(throughMaturityDate));
        if (futures.Count == 0)
            throw new InvalidOperationException($"No current or future futures contracts exist for '{symbol}'.");

        var snapshot = await store.GetSnapshotAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Instrument definitions have not been loaded. Run the instrument-definition refresh.");
        var underlyings = futures
            .Where(static future => future.InstrumentId != 0 && future.PublisherId != 0
                                    && !string.IsNullOrWhiteSpace(future.Dataset))
            .ToDictionary(
                static future => (future.Dataset, future.PublisherId, future.InstrumentId),
                static future => future);

        var options = new List<FuturesOptionContractReadModel>();
        foreach (var dataset in snapshot.Datasets
                     .Where(static dataset => !string.IsNullOrWhiteSpace(dataset))
                     .Distinct(StringComparer.Ordinal))
        {
            var storedFutures = await LoadSelectionsAsync(
                store, snapshot, dataset, symbol, false, at, cancellationToken).ConfigureAwait(false);
            foreach (var definition in storedFutures)
            {
                if (definition.InstrumentClass != "F" || definition.ExpirationUtc is not { } expiration)
                    continue;
                var maturity = DateOnly.FromDateTime(expiration.UtcDateTime);
                var canonical = futures.SingleOrDefault(future => future.LastTradeDate == maturity);
                if (canonical is null)
                    continue;
                underlyings[(definition.Dataset, definition.PublisherId, definition.InstrumentId)] =
                    canonical with
                    {
                        Dataset = definition.Dataset,
                        PublisherId = definition.PublisherId,
                        InstrumentId = definition.InstrumentId,
                        RawSymbol = definition.RawSymbol,
                        DefinitionTimestampUtc = definition.DefinitionTimestampUtc,
                        DefinitionDigest = definition.DefinitionDigest,
                        RawDefinitionReference = definition.RawDefinitionReference,
                        ExpirationUtc = definition.ExpirationUtc,
                        MultiplierValue = definition.Multiplier,
                        TickSize = definition.TickSize
                    };
            }

            var storedOptions = await LoadSelectionsAsync(
                store, snapshot, dataset, providerRoot, true, at, cancellationToken).ConfigureAwait(false);
            foreach (var definition in storedOptions)
            {
                if (definition.ExpirationUtc is not { } expiration)
                    continue;
                var expiry = DateOnly.FromDateTime(expiration.UtcDateTime);
                if (expiry < fromMaturityDate || expiry > throughMaturityDate)
                    continue;
                if (!underlyings.TryGetValue(
                        (definition.Dataset, definition.PublisherId, definition.UnderlyingInstrumentId),
                        out var underlying))
                {
                    throw new InvalidDataException(
                        $"Option definition '{definition.RawSymbol}' for {providerRoot}.OPT references " +
                        $"underlying instrument {definition.UnderlyingInstrumentId}, which cannot be joined " +
                        $"to the authoritative '{symbol}' futures curve from snapshot {snapshot.Id}.");
                }
                options.Add(MapToOption(definition, underlying, expiry));
            }
        }

        return options
            .DistinctBy(static option => option.ContractId)
            .OrderBy(static option => option.ContractMonth)
            .ThenBy(static option => option.StrikePriceDecimal)
            .ThenBy(static option => option.OptionType, StringComparer.Ordinal)
            .ThenBy(static option => option.ContractId, StringComparer.Ordinal)
            .ToArray();
    }

    static async Task<IReadOnlyList<InstrumentDefinitionSelection>> LoadSelectionsAsync(
        IInstrumentDefinitionStore store,
        InstrumentDefinitionSnapshot snapshot,
        string dataset,
        string root,
        bool options,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var definitions = new List<InstrumentDefinitionSelection>();
        string? continuationToken = null;
        do
        {
            var page = await store.GetSelectionPageAsync(
                new InstrumentDefinitionPageRequest
                {
                    Dataset = dataset,
                    Root = root,
                    Options = options,
                    SnapshotId = snapshot.Id,
                    ContinuationToken = continuationToken,
                    PageSize = PageSize
                },
                at,
                cancellationToken).ConfigureAwait(false);
            if (page.SnapshotId != snapshot.Id)
                throw new InvalidDataException(
                    "Instrument-definition snapshot changed during option-cache publication.");
            definitions.AddRange(page.Items);
            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);
        return definitions;
    }

    internal static FuturesOptionContractReadModel MapToOption(
        InstrumentDefinitionSelection definition,
        FuturesContractV3ReadModel underlying,
        DateOnly expiry)
    {
        if (definition.InstrumentClass is not ("C" or "P") || definition.Strike is not > 0
            || definition.InstrumentId == 0 || definition.PublisherId == 0
            || definition.UnderlyingInstrumentId != underlying.InstrumentId
            || definition.PublisherId != underlying.PublisherId
            || !string.Equals(definition.Dataset, underlying.Dataset, StringComparison.Ordinal))
            throw new InvalidDataException("A complete option definition and its exact underlying future are required.");

        var call = definition.InstrumentClass == "C";
        var strike = definition.Strike.Value;
        var expiration = definition.ExpirationUtc!.Value;
        decimal? underlyingMultiplier = null;
        if (decimal.TryParse(underlying.Multiplier, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var parsedUnderlyingMultiplier))
            underlyingMultiplier = parsedUnderlyingMultiplier;
        underlyingMultiplier ??= underlying.MultiplierValue;
        var providerEconomicsCompatible = definition.Multiplier is null or 1m or 50m
            && definition.TickSize is null or > 0;
        var reviewedEsDefinition = underlying.Symbol == "ES"
            && definition.Dataset == "GLBX.MDP3"
            && definition.Exchange == "XCME"
            && definition.Currency == "USD"
            && providerEconomicsCompatible
            && definition.DefinitionTimestampUtc.Offset == TimeSpan.Zero
            && definition.DefinitionTimestampUtc < expiration
            && definition.DefinitionDigest is { Length: 64 }
            && definition.DefinitionDigest.All(Uri.IsHexDigit)
            && !string.IsNullOrWhiteSpace(definition.RawDefinitionReference);
        var multiplier = reviewedEsDefinition ? 50m : definition.Multiplier ?? underlyingMultiplier;
        var mappingVersion = reviewedEsDefinition
            ? $"StoredInstrumentDefinition/v2/{definition.DefinitionDigest[..16]}"
            : "StoredInstrumentDefinition/v1";
        return new(
            FuturesOptionContractId.Create(
                underlying.Symbol,
                expiry,
                call ? OptionType.Call : OptionType.Put,
                strike),
            definition.RawSymbol,
            underlying.Symbol,
            definition.RawSymbol,
            "FOP",
            definition.Currency,
            definition.Exchange,
            multiplier?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            expiry,
            (double)strike,
            call ? "Call" : "Put")
        {
            SchemaVersion = 1,
            ReviewState = reviewedEsDefinition ? ReferenceReviewState.Reviewed : ReferenceReviewState.Draft,
            StrikePriceDecimal = strike,
            Dataset = definition.Dataset,
            PublisherId = definition.PublisherId,
            InstrumentId = definition.InstrumentId,
            RawSymbol = definition.RawSymbol,
            DefinitionTimestampUtc = definition.DefinitionTimestampUtc,
            DefinitionDigest = definition.DefinitionDigest,
            RawDefinitionReference = definition.RawDefinitionReference,
            ExpirationUtc = definition.ExpirationUtc,
            LastTradingUtc = definition.ExpirationUtc,
            MultiplierValue = multiplier,
            PriceScale = 1m,
            TickSize = reviewedEsDefinition ? .05m : definition.TickSize,
            MappingVersion = mappingVersion,
            UnderlyingContractId = underlying.ContractId,
            UnderlyingAssetType = ReferenceAssetType.Futures,
            UnderlyingInstrumentId = definition.UnderlyingInstrumentId,
            UnderlyingPublisherId = underlying.PublisherId,
            OptionRight = call ? ReferenceOptionRight.Call : ReferenceOptionRight.Put,
            ExchangeTimeZoneId = reviewedEsDefinition ? "America/New_York" : null,
            CalendarVersion = reviewedEsDefinition ? "IFM-MarketDates" : null,
            EvidenceId = reviewedEsDefinition ? definition.RawDefinitionReference : null,
            EffectiveFromUtc = reviewedEsDefinition ? definition.DefinitionTimestampUtc : null,
            EffectiveUntilUtc = reviewedEsDefinition ? expiration : null,
            ExerciseStyle = reviewedEsDefinition ? ReferenceExerciseStyle.European : ReferenceExerciseStyle.Unknown,
            SettlementStyle = reviewedEsDefinition ? ReferenceSettlementStyle.DeliveryOfFuture : ReferenceSettlementStyle.Unknown,
            PremiumStyle = reviewedEsDefinition ? ReferencePremiumStyle.FuturesStyleVariation : ReferencePremiumStyle.Unknown,
            DayCount = reviewedEsDefinition ? ReferenceDayCount.Actual365Fixed : ReferenceDayCount.Unknown,
            PremiumTickRule = reviewedEsDefinition ? ReferencePremiumTickRule.CmeEsGlobex358A : ReferencePremiumTickRule.Unknown,
            TickRuleVersion = reviewedEsDefinition ? OptionPremiumTicks.CmeEsGlobexVersion : null,
            ExerciseCutoffUtc = reviewedEsDefinition ? expiration : null
        };
    }

    static FuturesContractV3ReadModel MapToFuture(
        string symbol,
        InstrumentDefinitionSelection definition)
    {
        if (definition.InstrumentClass != "F" || definition.ExpirationUtc is not { } expiration
            || definition.InstrumentId == 0 || definition.PublisherId == 0)
            throw new InvalidDataException("A complete futures definition is required.");
        var maturity = DateOnly.FromDateTime(expiration.UtcDateTime);
        return new FuturesContractV3ReadModel(
            $"{symbol}{maturity:yyyyMMdd}",
            definition.RawSymbol,
            symbol,
            definition.RawSymbol,
            "FUT",
            definition.Currency,
            definition.Exchange,
            definition.Multiplier?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            maturity,
            false,
            false)
        {
            SchemaVersion = 1,
            ReviewState = ReferenceReviewState.Draft,
            Dataset = definition.Dataset,
            PublisherId = definition.PublisherId,
            InstrumentId = definition.InstrumentId,
            RawSymbol = definition.RawSymbol,
            DefinitionTimestampUtc = definition.DefinitionTimestampUtc,
            DefinitionDigest = definition.DefinitionDigest,
            RawDefinitionReference = definition.RawDefinitionReference,
            ExpirationUtc = definition.ExpirationUtc,
            LastTradingUtc = definition.ExpirationUtc,
            MultiplierValue = definition.Multiplier,
            PriceScale = 1m,
            TickSize = definition.TickSize,
            MappingVersion = "StoredInstrumentDefinition/v1"
        };
    }
}
