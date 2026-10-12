using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>
/// Discriminates a specific contract from a roll-aware futures continuation without inspecting string shape.
/// </summary>
[MessagePackObject]
public readonly record struct MarketSeriesIdentity
{
    /// <summary>Gets the populated identity variant.</summary>
    [Key(0)]
    public MarketSeriesIdentityKind Kind { get; init; }

    /// <summary>Gets the specific contract identity when <see cref="Kind"/> is <see cref="MarketSeriesIdentityKind.Contract"/>.</summary>
    [Key(1)]
    public string ContractId { get; init; }

    /// <summary>Gets the continuation identity when <see cref="Kind"/> is <see cref="MarketSeriesIdentityKind.FuturesContinuation"/>.</summary>
    [Key(2)]
    public FuturesSeriesId? FuturesSeriesId { get; init; }

    /// <summary>Initializes an empty value for serialization.</summary>
    public MarketSeriesIdentity() => ContractId = string.Empty;

    /// <summary>Initializes a discriminated market-series identity.</summary>
    /// <param name="kind">Populated identity variant.</param>
    /// <param name="contractId">Specific contract identity, when applicable.</param>
    /// <param name="futuresSeriesId">Futures continuation identity, when applicable.</param>
    [SerializationConstructor]
    public MarketSeriesIdentity(
        MarketSeriesIdentityKind kind,
        string contractId,
        FuturesSeriesId? futuresSeriesId)
    {
        Kind = kind;
        ContractId = contractId ?? string.Empty;
        FuturesSeriesId = futuresSeriesId;
    }

    /// <summary>Creates an identity for one specific futures contract.</summary>
    /// <param name="contractId">Canonical domain contract identity.</param>
    /// <returns>A specific-contract market-series identity.</returns>
    public static MarketSeriesIdentity ForContract(string contractId) =>
        new(MarketSeriesIdentityKind.Contract, contractId, null);

    /// <summary>Creates an identity for one roll-aware futures continuation.</summary>
    /// <param name="seriesId">Continuation identity.</param>
    /// <returns>A continuation market-series identity.</returns>
    public static MarketSeriesIdentity ForFuturesSeries(FuturesSeriesId seriesId) =>
        new(MarketSeriesIdentityKind.FuturesContinuation, string.Empty, seriesId);

    /// <summary>Formats the explicitly discriminated identity.</summary>
    /// <returns>A stable, escaped identity string.</returns>
    public string Format() => Kind switch
    {
        MarketSeriesIdentityKind.Contract => $"contract:{Uri.EscapeDataString(ContractId)}",
        MarketSeriesIdentityKind.FuturesContinuation when FuturesSeriesId is { } value =>
            $"futures:{Uri.EscapeDataString(value.Format())}",
        _ => "unknown:"
    };

    /// <summary>Parses a formatted market-series identity.</summary>
    /// <param name="value">Formatted identity.</param>
    /// <returns>The parsed identity.</returns>
    /// <exception cref="FormatException">Thrown when the value is malformed.</exception>
    public static MarketSeriesIdentity Parse(string value) =>
        TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException("MarketSeriesIdentity is malformed.");

    /// <summary>Attempts to parse a formatted market-series identity.</summary>
    /// <param name="value">Formatted identity.</param>
    /// <param name="identity">Receives the parsed identity.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out MarketSeriesIdentity identity)
    {
        identity = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0) return false;
        try
        {
            var payload = Uri.UnescapeDataString(value[(separator + 1)..]);
            identity = value[..separator] switch
            {
                "contract" when !string.IsNullOrWhiteSpace(payload) => ForContract(payload),
                "futures" when global::TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common.FuturesSeriesId.TryParse(
                    payload,
                    out var seriesId) => ForFuturesSeries(seriesId),
                _ => default
            };
            return new MarketSeriesIdentityValidationRules().Execute(identity).Length == 0;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    /// <summary>Returns the stable formatted identity.</summary>
    public override string ToString() => Format();
}
