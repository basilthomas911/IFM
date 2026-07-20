using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketData.ViewModels;

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

/// <summary>
/// Provides validation rules for yield curve rate data, ensuring that all required fields are present and contain valid
/// values.
/// </summary>
/// <remarks>This class implements validation logic for the YieldCurveRateReadModel, checking that date and rate
/// fields are within acceptable ranges and not invalid (such as NaN or out-of-range dates). Use this class to validate
/// yield curve rate entries before processing or persisting them. Inherits from BaseValidationRules and implements
/// IValidationRules for yield curve rate models.</remarks>
public class YieldCurveRateValidationRules : BaseValidationRules, IValidationRules<YieldCurveRateReadModel>
{

    public YieldCurveRateValidationRules()
    {
    }

    public ValidationError[] Execute(YieldCurveRateReadModel e) => Validate(e, new YieldCurveRateValidator());

    private class YieldCurveRateValidator : AbstractValidator<YieldCurveRateReadModel>
    {
        public YieldCurveRateValidator()
        {
            RuleFor(x => x.ValueDate).Must(e => e != DateOnly.MinValue && e != DateOnly.MaxValue).WithMessage("YieldCurveRate.ValueDate is invalid");
            RuleFor(x => x.OneMonth).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.OneMonth for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.TwoMonth).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.TwoMonth for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.ThreeMonth).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.ThreeMonth for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.SixMonth).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.SixMonth for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.OneYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.OneYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.TwoYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.TwoYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.ThreeYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.ThreeYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.FiveYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.FiveYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.SevenYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.SevenYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.TenYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.TenYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.TwentyYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.TwentyYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
            RuleFor(x => x.ThirtyYear).Must(e => !double.IsNaN(e)).GreaterThanOrEqualTo(0.0).WithMessage(e => $"YieldCurveRate.ThirtyYear for {e.ValueDate:yyyy-MM-dd} is not a valid number");
        }

        public override ValidationResult Validate(ValidationContext<YieldCurveRateReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("YieldCurveRate", "YieldCurveRate instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }

    }
}
