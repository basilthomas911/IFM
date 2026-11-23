using System.Globalization;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Cold rollover discovery from the published provider snapshot; never fabricates expiry dates.</summary>
public sealed class StoredFuturesContractDiscovery(
    IInstrumentDefinitionStore definitions,
    ICurrentFuturesContractCatalog catalog,
    IDiscoveredFuturesContractPublisher publisher,
    TimeProvider clock) : ICurrentFuturesContractDiscovery
{
    public async Task EnsureEligibleAsync(string symbol, DateOnly valueDate, int count, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var existing = await catalog.GetFuturesContractsBySymbolAsync(symbol, cancellationToken).ConfigureAwait(false);
        var selected = DatabentoContractAuthority.Eligible(existing, valueDate).Where(value => value.Symbol == symbol)
            .Select(value => value.ContractId).ToHashSet(StringComparer.Ordinal);
        if (selected.Count >= count) return;
        var snapshot = await definitions.GetSnapshotAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No published provider snapshot is available to register the next {symbol} contract.");
        var at = clock.GetUtcNow();
        var candidates = new List<InstrumentDefinitionSelection>();
        foreach (var dataset in snapshot.Datasets.Distinct(StringComparer.Ordinal))
        {
            string? cursor = null;
            var cursors = new HashSet<string>(StringComparer.Ordinal);
            do
            {
                var page = await definitions.GetSelectionPageAsync(new()
                {
                    Dataset = dataset, Root = symbol, Options = false, SnapshotId = snapshot.Id,
                    PageSize = 200, ContinuationToken = cursor
                }, at, cancellationToken).ConfigureAwait(false);
                if (page.SnapshotId != snapshot.Id)
                    throw new InvalidDataException("Provider snapshot changed during futures rollover discovery.");
                candidates.AddRange(page.Items.Where(value => IsEligible(value, symbol, dataset, snapshot.Id, valueDate, at)));
                cursor = page.ContinuationToken;
                if (cursor is not null && !cursors.Add(cursor))
                    throw new InvalidDataException("Provider futures discovery returned a repeated page cursor.");
            } while (cursor is not null);
        }

        // Existing catalog rows take precedence. Missing rows retain exact provider identity and
        // inherit only established root-level currency/multiplier when the provider omits them.
        var missing = candidates.OrderBy(value => value.ExpirationUtc).ThenBy(value => value.RawSymbol, StringComparer.Ordinal)
            .DistinctBy(value => DateOnly.FromDateTime(value.ExpirationUtc!.Value.UtcDateTime))
            .Where(value => !selected.Contains(Identity(value))).Take(Math.Max(0, count - selected.Count))
            .Select(value => Map(value, existing)).ToArray();
        if (selected.Count + missing.Length < count)
            throw new InvalidOperationException($"Published provider definitions do not contain {count} eligible {symbol} futures after {valueDate:yyyy-MM-dd}.");
        foreach (var contract in missing)
            await publisher.PublishAsync(contract, cancellationToken).ConfigureAwait(false);

        // Command acceptance can precede projection delivery. Do not publish a rollover pair
        // until every discovered contract is visible in the committed Securities catalog.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var committed = await catalog.GetFuturesContractsBySymbolAsync(symbol, deadline.Token).ConfigureAwait(false);
            if (missing.All(value => committed.Any(row => row.Id == value.Id))) return;
            await Task.Delay(TimeSpan.FromMilliseconds(50), deadline.Token).ConfigureAwait(false);
        }
    }

    static bool IsEligible(InstrumentDefinitionSelection value, string symbol, string dataset, Guid snapshot,
        DateOnly valueDate, DateTimeOffset at)
        => value.SnapshotId == snapshot && value.Dataset == dataset && value.Root == symbol
            && value.InstrumentClass == "F" && !value.Deleted && value.InstrumentId != 0 && value.PublisherId != 0
            && value.RawSymbol.StartsWith(symbol, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(value.Exchange)
            && value.ExpirationUtc is { } expiry && expiry > at && DateOnly.FromDateTime(expiry.UtcDateTime) > valueDate
            && (value.ActivationUtc is null || value.ActivationUtc <= at) && value.DefinitionTimestampUtc <= at
            && value.DefinitionDigest.Length == 64 && value.DefinitionDigest.All(char.IsAsciiHexDigit)
            && !string.IsNullOrWhiteSpace(value.RawDefinitionReference);

    static string Identity(InstrumentDefinitionSelection value)
        => string.Create(CultureInfo.InvariantCulture, $"{value.Root}{DateOnly.FromDateTime(value.ExpirationUtc!.Value.UtcDateTime):yyyyMMdd}");

    static FuturesContractV3ReadModel Map(InstrumentDefinitionSelection value, ICollection<FuturesContractV3ReadModel> existing)
    {
        string RootValue(Func<FuturesContractV3ReadModel, string> select)
        {
            var values = existing.Where(row => row.Symbol == value.Root && row.SecurityType == "FUT" && row.Exchange == value.Exchange)
                .Select(select).Where(text => !string.IsNullOrWhiteSpace(text)).Distinct(StringComparer.Ordinal).ToArray();
            return values.Length == 1 ? values[0]
                : throw new InvalidDataException($"No unambiguous catalog metadata exists for {value.RawSymbol}.");
        }
        var currency = string.IsNullOrWhiteSpace(value.Currency) ? RootValue(row => row.Currency) : value.Currency;
        var multiplier = value.Multiplier is > 0 ? value.Multiplier.Value.ToString(CultureInfo.InvariantCulture)
            : RootValue(row => row.Multiplier);
        return new(Identity(value), value.RawSymbol, value.Root, value.RawSymbol, "FUT", currency,
            value.Exchange, multiplier, DateOnly.FromDateTime(value.ExpirationUtc!.Value.UtcDateTime), false, false)
        {
            SchemaVersion = 1, ReviewState = ReferenceReviewState.Draft,
            Dataset = value.Dataset, PublisherId = value.PublisherId, InstrumentId = value.InstrumentId,
            RawSymbol = value.RawSymbol, DefinitionTimestampUtc = value.DefinitionTimestampUtc,
            DefinitionDigest = value.DefinitionDigest, RawDefinitionReference = value.RawDefinitionReference,
            ExpirationUtc = value.ExpirationUtc, LastTradingUtc = value.ExpirationUtc,
            MultiplierValue = value.Multiplier, TickSize = value.TickSize,
            MappingVersion = "StoredFuturesRollover/v1"
        };
    }
}
