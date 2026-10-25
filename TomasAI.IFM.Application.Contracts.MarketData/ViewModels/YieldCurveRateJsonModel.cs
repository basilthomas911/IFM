using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable JSON import model for yield curve data points (external feed).
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys;
/// includes a mapper to the internal YieldCurveRateReadModel.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record YieldCurveRateJsonModel
{
    /// <summary>Raw timestamp of the yield curve snapshot.</summary>
    [Key(0)]
    public DateTime Date { get; init; }

    /// <summary>1-month rate.</summary>
    [Key(1)]
    public double Month1 { get; init; }

    /// <summary>2-month rate.</summary>
    [Key(2)]
    public double Month2 { get; init; }

    /// <summary>3-month rate.</summary>
    [Key(3)]
    public double Month3 { get; init; }

    /// <summary>6-month rate.</summary>
    [Key(4)]
    public double Month6 { get; init; }

    /// <summary>1-year rate.</summary>
    [Key(5)]
    public double Year1 { get; init; }

    /// <summary>2-year rate.</summary>
    [Key(6)]
    public double Year2 { get; init; }

    /// <summary>3-year rate.</summary>
    [Key(7)]
    public double Year3 { get; init; }

    /// <summary>5-year rate.</summary>
    [Key(8)]
    public double Year5 { get; init; }

    /// <summary>7-year rate.</summary>
    [Key(9)]
    public double Year7 { get; init; }

    /// <summary>10-year rate.</summary>
    [Key(10)]
    public double Year10 { get; init; }

    /// <summary>20-year rate.</summary>
    [Key(11)]
    public double Year20 { get; init; }

    /// <summary>30-year rate.</summary>
    [Key(12)]
    public double Year30 { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public YieldCurveRateJsonModel() { }

    /// <summary>
    /// Full constructor for a JSON import model instance.
    /// </summary>
    public YieldCurveRateJsonModel(
        DateTime date,
        double month1,
        double month2,
        double month3,
        double month6,
        double year1,
        double year2,
        double year3,
        double year5,
        double year7,
        double year10,
        double year20,
        double year30)
    {
        Date = date;
        Month1 = month1;
        Month2 = month2;
        Month3 = month3;
        Month6 = month6;
        Year1 = year1;
        Year2 = year2;
        Year3 = year3;
        Year5 = year5;
        Year7 = year7;
        Year10 = year10;
        Year20 = year20;
        Year30 = year30;
    }

    /// <summary>
    /// Maps the external JSON model to the internal <see cref="YieldCurveRateReadModel"/>.
    /// </summary>
    public YieldCurveRateReadModel ToViewModel()
        => new YieldCurveRateReadModel(
            valueDate: DateOnly.FromDateTime(Date),
            oneMonth: Month1,
            twoMonth: Month2,
            threeMonth: Month3,
            sixMonth: Month6,
            oneYear: Year1,
            twoYear: Year2,
            threeYear: Year3,
            fiveYear: Year5,
            sevenYear: Year7,
            tenYear: Year10,
            twentyYear: Year20,
            thirtyYear: Year30);
}
