using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

/// <summary>Identifies one immutable closed analytics observation.</summary>
[MessagePackObject]
public readonly record struct FuturesTradeSessionBarId
{
    /// <summary>Gets the deterministic observation value.</summary>
    [Key(0)] public Guid Value { get; init; }

    /// <summary>Initializes an observation identity.</summary>
    /// <param name="value">Deterministic observation value.</param>
    [SerializationConstructor]
    public FuturesTradeSessionBarId(Guid value) => Value = value;

    /// <summary>Creates an identity from the complete immutable observation lineage.</summary>
    /// <param name="seriesIdentity">Specific-contract or continuation identity.</param>
    /// <param name="timeFrame">Observation timeframe.</param>
    /// <param name="intervalEndUtc">Exclusive UTC interval end.</param>
    /// <param name="lastSourceSequence">Last accepted source sequence.</param>
    /// <returns>A deterministic observation identity.</returns>
    public static FuturesTradeSessionBarId Create(
        MarketSeriesIdentity seriesIdentity,
        TimeFrameType timeFrame,
        DateTimeOffset intervalEndUtc,
        long lastSourceSequence)
    {
        if (new MarketSeriesIdentityValidationRules().Execute(seriesIdentity).Length != 0)
            throw new ArgumentException("A valid market series identity is required.", nameof(seriesIdentity));
        if (timeFrame == TimeFrameType.None || !Enum.IsDefined(timeFrame))
            throw new ArgumentOutOfRangeException(nameof(timeFrame));
        if (intervalEndUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Interval end must be UTC.", nameof(intervalEndUtc));
        if (lastSourceSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(lastSourceSequence));

        var canonical = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{seriesIdentity.Format()}|{timeFrame}|{intervalEndUtc:O}|{lastSourceSequence}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return new(new Guid(hash.AsSpan(0, 16)));
    }

    /// <summary>Parses a formatted observation identity.</summary>
    /// <param name="value">GUID text.</param>
    /// <returns>The parsed observation identity.</returns>
    public static FuturesTradeSessionBarId Parse(string value) => new(Guid.Parse(value));

    /// <summary>Attempts to parse a formatted observation identity.</summary>
    /// <param name="value">GUID text.</param>
    /// <param name="observationId">Receives the parsed identity.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out FuturesTradeSessionBarId observationId)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            observationId = new(parsed);
            return true;
        }
        observationId = default;
        return false;
    }

    /// <summary>Formats the observation identity without separators.</summary>
    public override string ToString() => Value.ToString("N");
}

/// <summary>Validates a deterministic futures analytics observation identity.</summary>
public sealed class FuturesTradeSessionBarIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesTradeSessionBarId>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied observation identity.</summary>
    /// <param name="value">Identity to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(FuturesTradeSessionBarId value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesTradeSessionBarId>
    {
        public Validator() => RuleFor(x => x.Value).NotEmpty();
    }
}

/// <summary>
/// Identifies the coordinator stream that closes observations for one market series and timeframe.
/// </summary>
[MessagePackObject]
public readonly record struct FuturesTradeSessionBarEntityId : IActorEntityId
{
    /// <summary>Gets the specific-contract or continuation identity.</summary>
    [Key(0)] public MarketSeriesIdentity MarketSeriesIdentity { get; init; }

    /// <summary>Gets the observation timeframe.</summary>
    [Key(1)] public TimeFrameType TimeFrame { get; init; }

    /// <summary>Initializes a coordinator stream identity.</summary>
    /// <param name="marketSeriesIdentity">Specific-contract or continuation identity.</param>
    /// <param name="timeFrame">Observation timeframe.</param>
    [SerializationConstructor]
    public FuturesTradeSessionBarEntityId(
        MarketSeriesIdentity marketSeriesIdentity,
        TimeFrameType timeFrame)
    {
        MarketSeriesIdentity = marketSeriesIdentity;
        TimeFrame = timeFrame;
    }

    /// <summary>Formats the stable actor-routing identity.</summary>
    /// <returns>An escaped market-series identity followed by its timeframe.</returns>
    public string Format() =>
        $"{Uri.EscapeDataString(MarketSeriesIdentity.Format())}|{TimeFrame}";

    /// <summary>Parses a formatted actor-routing identity.</summary>
    /// <param name="value">Formatted identity.</param>
    /// <returns>The parsed entity identity.</returns>
    /// <exception cref="FormatException">Thrown when the value is malformed.</exception>
    public static FuturesTradeSessionBarEntityId Parse(string value) =>
        TryParse(value, out var parsed)
            ? parsed
            : throw new FormatException("FuturesTradeSessionBarEntityId is malformed.");

    /// <summary>Attempts to parse a formatted actor-routing identity.</summary>
    /// <param name="value">Formatted identity.</param>
    /// <param name="entityId">Receives the parsed identity.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out FuturesTradeSessionBarEntityId entityId)
    {
        entityId = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var separator = value.LastIndexOf('|');
        if (separator <= 0
            || !Enum.TryParse<TimeFrameType>(value[(separator + 1)..], out var timeFrame)
            || !MarketSeriesIdentity.TryParse(
                Uri.UnescapeDataString(value[..separator]),
                out var seriesIdentity))
            return false;
        entityId = new(seriesIdentity, timeFrame);
        return new FuturesTradeSessionBarEntityIdValidationRules().Execute(entityId).Length == 0;
    }

    /// <summary>Returns the stable actor-routing identity.</summary>
    public override string ToString() => Format();
}

/// <summary>Validates a futures analytics observation coordinator identity.</summary>
public sealed class FuturesTradeSessionBarEntityIdValidationRules
    : BaseValidationRules, IValidationStructRules<FuturesTradeSessionBarEntityId>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied entity identity.</summary>
    /// <param name="value">Identity to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(FuturesTradeSessionBarEntityId value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesTradeSessionBarEntityId>
    {
        public Validator()
        {
            RuleFor(x => x.MarketSeriesIdentity)
                .Must(x => new MarketSeriesIdentityValidationRules().Execute(x).Length == 0);
            RuleFor(x => x.TimeFrame).IsInEnum().NotEqual(TimeFrameType.None);
        }
    }
}
