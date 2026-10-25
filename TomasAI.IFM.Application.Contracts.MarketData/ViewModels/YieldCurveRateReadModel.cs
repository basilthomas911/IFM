using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a yield curve snapshot for a specific value date,
/// including short- and long-term rates (1M through 30Y).
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived identifiers
/// are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record YieldCurveRateReadModel
{
    /// <summary>The value (as-of) date for the yield curve snapshot.</summary>
    [Key(0)]
    public DateOnly ValueDate { get; init; }

    /// <summary>1-month rate.</summary>
    [Key(1)]
    public double OneMonth { get; init; }

    /// <summary>2-month rate.</summary>
    [Key(2)]
    public double TwoMonth { get; init; }

    /// <summary>3-month rate.</summary>
    [Key(3)]
    public double ThreeMonth { get; init; }

    /// <summary>6-month rate.</summary>
    [Key(4)]
    public double SixMonth { get; init; }

    /// <summary>1-year rate.</summary>
    [Key(5)]
    public double OneYear { get; init; }

    /// <summary>2-year rate.</summary>
    [Key(6)]
    public double TwoYear { get; init; }

    /// <summary>3-year rate.</summary>
    [Key(7)]
    public double ThreeYear { get; init; }

    /// <summary>5-year rate.</summary>
    [Key(8)]
    public double FiveYear { get; init; }

    /// <summary>7-year rate.</summary>
    [Key(9)]
    public double SevenYear { get; init; }

    /// <summary>10-year rate.</summary>
    [Key(10)]
    public double TenYear { get; init; }

    /// <summary>20-year rate.</summary>
    [Key(11)]
    public double TwentyYear { get; init; }

    /// <summary>30-year rate.</summary>
    [Key(12)]
    public double ThirtyYear { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public YieldCurveRateReadModel() { }

    /// <summary>
    /// Full constructor to create a yield curve snapshot.
    /// </summary>
    public YieldCurveRateReadModel(
        DateOnly valueDate,
        double oneMonth,
        double twoMonth,
        double threeMonth,
        double sixMonth,
        double oneYear,
        double twoYear,
        double threeYear,
        double fiveYear,
        double sevenYear,
        double tenYear,
        double twentyYear,
        double thirtyYear)
    {
        ValueDate = valueDate;
        OneMonth = oneMonth;
        TwoMonth = twoMonth;
        ThreeMonth = threeMonth;
        SixMonth = sixMonth;
        OneYear = oneYear;
        TwoYear = twoYear;
        ThreeYear = threeYear;
        FiveYear = fiveYear;
        SevenYear = sevenYear;
        TenYear = tenYear;
        TwentyYear = twentyYear;
        ThirtyYear = thirtyYear;
    }

    /// <summary>Derived identifier for the value date (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public YieldCurveRateId Id => new(ValueDate);

    /// <summary>Derived entity identifier (year-based) (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public YieldCurveRateEntityId EntityId => new(ValueDate.Year);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => ValueDate > DateOnly.MinValue;
}
