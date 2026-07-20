using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketData.ViewModels;

/// <summary>
/// Represents a normal curve lookup table used to derive probabilities and standard deviations
/// for option spread analytics based on Bollinger band positioning.
/// </summary>
/// <remarks>
/// MessagePack serializable. Stores an array of <see cref="NormalCurveDataReadModel"/> entries representing
/// cumulative percentages across standard deviation indices. Provides helper methods to compute put/call spread
/// probabilities and corresponding standard deviations given an asset price and Bollinger bands.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record NormalCurveTableReadModel
{
    /// <summary>
    /// Normal curve table where index corresponds to a percentile position within the band
    /// and <see cref="NormalCurveDataReadModel.Percent"/> contains the percentage value.
    /// </summary>
    [Key(0)]
    public NormalCurveDataReadModel[] NormalCurveTable { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public NormalCurveTableReadModel() { }

    /// <summary>
    /// Initializes the normal curve table with the provided entries.
    /// </summary>
    /// <param name="normalCurveTable">Array of normal curve entries.</param>
    public NormalCurveTableReadModel(NormalCurveDataReadModel[] normalCurveTable)
    {
        NormalCurveTable = normalCurveTable;
    }

    /// <summary>
    /// Computes the put option spread probability based on asset position relative to Bollinger bands.
    /// </summary>
    /// <param name="assetPrice">Current asset price.</param>
    /// <param name="upperBand">Bollinger upper band.</param>
    /// <param name="lowerBand">Bollinger lower band.</param>
    /// <returns>Probability in [0,1].</returns>
    public double GetPutOptionSpreadProbability(double assetPrice, double upperBand, double lowerBand)
    {
        var optionSpreadProb = 0.0;
        try
        {
            if (assetPrice > upperBand)
                optionSpreadProb = NormalCurveTable.First().Percent * 2.0 / 100.0;
            else if (assetPrice < lowerBand)
                optionSpreadProb = NormalCurveTable.Last().Percent * 2.0 / 100.0;
            else if ((upperBand - lowerBand) != 0.0)
            {
                var index = System.Convert.ToInt32((upperBand - assetPrice) / (upperBand - lowerBand) * 100);
                optionSpreadProb = NormalCurveTable[index].Percent * 2.0 / 100.0;
            }
        }
        catch { }
        return optionSpreadProb;
    }

    /// <summary>
    /// Computes the put option spread standard deviation adjustment.
    /// </summary>
    /// <param name="assetPrice">Current asset price.</param>
    /// <param name="upperBand">Bollinger upper band.</param>
    /// <param name="lowerBand">Bollinger lower band.</param>
    /// <returns>Standard deviation adjustment in [1,2].</returns>
    public double GetPutOptionSpreadStdDev(double assetPrice, double upperBand, double lowerBand)
    {
        var stdDev = 1.0;
        try
        {
            if (assetPrice > upperBand)
                stdDev = 1.0;
            else if (assetPrice < lowerBand)
                stdDev = 2.0;
            else if ((upperBand - lowerBand) != 0.0)
                stdDev = 1.0 + (upperBand - assetPrice) / (upperBand - lowerBand);
        }
        catch { }
        return stdDev;
    }

    /// <summary>
    /// Computes the call option spread probability based on asset position relative to Bollinger bands.
    /// </summary>
    /// <param name="assetPrice">Current asset price.</param>
    /// <param name="upperBand">Bollinger upper band.</param>
    /// <param name="lowerBand">Bollinger lower band.</param>
    /// <returns>Probability in [0,1].</returns>
    public double GetCallOptionSpreadProbability(double assetPrice, double upperBand, double lowerBand)
    {
        var optionSpreadProb = 0.0;
        try
        {
            if (assetPrice < lowerBand)
                optionSpreadProb = NormalCurveTable.First().Percent * 2.0 / 100.0;
            else if (assetPrice > upperBand)
                optionSpreadProb = NormalCurveTable.Last().Percent * 2.0 / 100.0;
            else if ((upperBand - lowerBand) != 0.0)
            {
                var index = System.Convert.ToInt32((assetPrice - lowerBand) / (upperBand - lowerBand) * 100);
                optionSpreadProb = NormalCurveTable[index].Percent * 2.0 / 100.0;
            }
        }
        catch { }
        return optionSpreadProb;
    }

    /// <summary>
    /// Computes the call option spread standard deviation adjustment.
    /// </summary>
    /// <param name="assetPrice">Asset price.</param>
    /// <param name="upperBand">Bollinger upper band.</param>
    /// <param name="lowerBand">Bollinger lower band.</param>
    /// <returns>Standard deviation adjustment in [1,2].</returns>
    public double GetCallOptionSpreadStdDev(double assetPrice, double upperBand, double lowerBand)
    {
        var stdDev = 0.0;
        try
        {
            if (assetPrice < lowerBand)
                stdDev = 1.0;
            else if (assetPrice > upperBand)
                stdDev = 2.0;
            else if ((upperBand - lowerBand) != 0.0)
                stdDev = 1.0 + (assetPrice - lowerBand) / (upperBand - lowerBand);
        }
        catch { }
        return stdDev;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => NormalCurveTable != null && NormalCurveTable.Length > 0;
}

/// <summary>
/// Provides FluentValidation rules for <see cref="NormalCurveTableReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates normal curve table ensuring the table array is not null or empty.
/// </remarks>
public class NormalCurveTableReadModelValidationRules : BaseValidationRules, IValidationRules<NormalCurveTableReadModel>
{
    public const string InstanceErrorMessage = "NormalCurveTableReadModel instance is null";
    public const string NormalCurveTableErrorMessage = "NormalCurveTableReadModel.NormalCurveTable is required and must not be empty";

    /// <summary>
    /// Executes validation rules against the specified NormalCurveTableReadModel instance.
    /// </summary>
    /// <param name="normalCurveTable">The normal curve table to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(NormalCurveTableReadModel normalCurveTable)
        => Validate(normalCurveTable, new NormalCurveTableReadModelValidator());

    /// <summary>
    /// Internal FluentValidation validator for NormalCurveTableReadModel.
    /// </summary>
    class NormalCurveTableReadModelValidator : AbstractValidator<NormalCurveTableReadModel>
    {
        public NormalCurveTableReadModelValidator()
        {
            // NormalCurveTable validation
            RuleFor(x => x.NormalCurveTable)
                .NotNull()
                .NotEmpty()
                .WithMessage(NormalCurveTableErrorMessage);
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        public override ValidationResult Validate(ValidationContext<NormalCurveTableReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("NormalCurveTableReadModel", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}