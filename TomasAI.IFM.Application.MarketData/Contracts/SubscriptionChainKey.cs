using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Immutable exact discovery universe. Provider mapping/expiry validation is a separate admission step.</summary>
public sealed class SubscriptionChainKey : IEquatable<SubscriptionChainKey>
{
    public const int MaximumContracts = 2048;
    public SubscriptionTickerKey Underlying { get; }
    public DateOnly MaturityDate { get; }
    public DateOnly ValueDate { get; }
    public IReadOnlyList<SubscriptionTickerKey> Options { get; }
    public string ContractSetDigest { get; }

    public SubscriptionChainKey(SubscriptionTickerKey underlying, DateOnly maturityDate,
        DateOnly valueDate, IEnumerable<SubscriptionTickerKey> options)
    {
        ArgumentNullException.ThrowIfNull(underlying);
        ArgumentNullException.ThrowIfNull(options);
        if (underlying.AssetKind != SubscriptionAssetKind.Futures || valueDate == default
            || maturityDate < valueDate) throw new ArgumentException("Chain dates/underlying are invalid.",nameof(underlying));
        var copy = options.Take(MaximumContracts + 1).ToArray();
        if (copy.Length is < 1 or > MaximumContracts || copy.Any(item => item is null
                || item.AssetKind != SubscriptionAssetKind.FuturesOption
                || !string.Equals(item.ProviderScope,underlying.ProviderScope,StringComparison.Ordinal) || !string.Equals(item.Dataset,underlying.Dataset,StringComparison.Ordinal)
                || !string.Equals(item.Schema,underlying.Schema,StringComparison.Ordinal))
            || copy.Distinct().Count() != copy.Length)
            throw new ArgumentException("A chain requires bounded unique options in the underlying routing scope.",nameof(options));
        Underlying = underlying;
        MaturityDate = maturityDate;
        ValueDate = valueDate;
        Options = Array.AsReadOnly(copy.OrderBy(item => item.ContractId, StringComparer.Ordinal).ToArray());
        ContractSetDigest = SubscriptionIdentity.Digest(writer =>
        {
            SubscriptionIdentity.Write(writer, underlying);
            writer.Write(maturityDate.DayNumber);
            writer.Write(valueDate.DayNumber);
            writer.Write(Options.Count);
            foreach (var option in Options) SubscriptionIdentity.Write(writer, option);
        });
    }

    public bool Equals(SubscriptionChainKey? other) => ReferenceEquals(this, other) || other is not null
        && Underlying == other.Underlying && MaturityDate == other.MaturityDate
        && ValueDate == other.ValueDate && string.Equals(ContractSetDigest,other.ContractSetDigest,StringComparison.Ordinal) && Options.SequenceEqual(other.Options);
    public override bool Equals(object? obj) => obj is SubscriptionChainKey other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ContractSetDigest);
}
