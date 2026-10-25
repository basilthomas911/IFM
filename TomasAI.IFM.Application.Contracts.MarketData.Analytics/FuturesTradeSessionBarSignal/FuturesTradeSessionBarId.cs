using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

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
        ArgumentOutOfRangeException.ThrowIfNegative(lastSourceSequence);

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
