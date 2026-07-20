using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Model;

/// <summary>
/// Represents a yield curve rate snapshot for a specific value date, including short- and long-term rates.
/// </summary>
/// <remarks>A yield curve rate contains rates for various maturities from 1 month to 30 years.
/// This class encapsulates the key attributes of a yield curve snapshot.</remarks>
public class YieldCurveRateModel
{
    public DateOnly ValueDate { get; }
    public double OneMonth { get; }
    public double TwoMonth { get; }
    public double ThreeMonth { get; }
    public double SixMonth { get; }
    public double OneYear { get; }
    public double TwoYear { get; }
    public double ThreeYear { get; }
    public double FiveYear { get; }
    public double SevenYear { get; }
    public double TenYear { get; }
    public double TwentyYear { get; }
    public double ThirtyYear { get; }

    public YieldCurveRateModel(
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

    public YieldCurveRateModel(YieldCurveRateReadModel model)
        : this(
            model.ValueDate,
            model.OneMonth,
            model.TwoMonth,
            model.ThreeMonth,
            model.SixMonth,
            model.OneYear,
            model.TwoYear,
            model.ThreeYear,
            model.FiveYear,
            model.SevenYear,
            model.TenYear,
            model.TwentyYear,
            model.ThirtyYear)
    {
    }

    public YieldCurveRateReadModel ToViewModel()
        => new(
            valueDate: ValueDate,
            oneMonth: OneMonth,
            twoMonth: TwoMonth,
            threeMonth: ThreeMonth,
            sixMonth: SixMonth,
            oneYear: OneYear,
            twoYear: TwoYear,
            threeYear: ThreeYear,
            fiveYear: FiveYear,
            sevenYear: SevenYear,
            tenYear: TenYear,
            twentyYear: TwentyYear,
            thirtyYear: ThirtyYear);
}
