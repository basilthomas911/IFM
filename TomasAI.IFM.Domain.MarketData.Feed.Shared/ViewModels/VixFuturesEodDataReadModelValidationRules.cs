using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>
/// Provides FluentValidation rules for <see cref="VixFuturesEodDataReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates VIX futures EOD data ensuring all required fields are present and valid,
/// including OHLC prices and volume consistency checks.
/// </remarks>
public class VixFuturesEodDataReadModelValidationRules : BaseValidationRules, IValidationRules<VixFuturesEodDataReadModel>
{
    public const string InstanceErrorMessage = "VixFuturesEodDataReadModel instance is null";
    public const string ContractIdErrorMessage = "VixFuturesEodDataReadModel.ContractId is required";
    public const string ValueDateErrorMessage = "VixFuturesEodDataReadModel.ValueDate is invalid";
    public const string OpenPriceErrorMessage = "VixFuturesEodDataReadModel.OpenPrice must be greater than zero";
    public const string HighPriceErrorMessage = "VixFuturesEodDataReadModel.HighPrice must be greater than zero";
    public const string LowPriceErrorMessage = "VixFuturesEodDataReadModel.LowPrice must be greater than zero";
    public const string ClosePriceErrorMessage = "VixFuturesEodDataReadModel.ClosePrice must be greater than zero";
    public const string VolumeErrorMessage = "VixFuturesEodDataReadModel.Volume must be non-negative";
    public const string HighLowErrorMessage = "VixFuturesEodDataReadModel.HighPrice must be >= LowPrice";
    public const string OhlcRangeErrorMessage = "VixFuturesEodDataReadModel OHLC prices must be within High/Low range";

    /// <summary>
    /// Executes validation rules against the specified VixFuturesEodDataReadModel instance.
    /// </summary>
    /// <param name="vixEodData">The VIX futures EOD data to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(VixFuturesEodDataReadModel vixEodData)
        => Validate(vixEodData, new VixFuturesEodDataReadModelValidator());

    /// <summary>
    /// Internal FluentValidation validator for VixFuturesEodDataReadModel.
    /// </summary>
    sealed class VixFuturesEodDataReadModelValidator : AbstractValidator<VixFuturesEodDataReadModel>
    {
        public VixFuturesEodDataReadModelValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage(ContractIdErrorMessage);

            // ValueDate validation
            RuleFor(x => x.ValueDate)
                .Must(valueDate => valueDate != DateOnly.MinValue && valueDate != DateOnly.MaxValue)
                .WithMessage(ValueDateErrorMessage);

            // OpenPrice validation
            RuleFor(x => x.OpenPrice)
                .GreaterThan(0)
                .WithMessage(OpenPriceErrorMessage);

            // HighPrice validation
            RuleFor(x => x.HighPrice)
                .GreaterThan(0)
                .WithMessage(HighPriceErrorMessage);

            // LowPrice validation
            RuleFor(x => x.LowPrice)
                .GreaterThan(0)
                .WithMessage(LowPriceErrorMessage);

            // ClosePrice validation
            RuleFor(x => x.ClosePrice)
                .GreaterThan(0)
                .WithMessage(ClosePriceErrorMessage);

            // Volume validation
            RuleFor(x => x.Volume)
                .GreaterThanOrEqualTo(0)
                .WithMessage(VolumeErrorMessage);

            // High >= Low validation
            RuleFor(x => x)
                .Must(x => x.HighPrice >= x.LowPrice)
                .WithMessage(HighLowErrorMessage);

            // OHLC range validation
            RuleFor(x => x)
                .Must(x => x.OpenPrice >= x.LowPrice && x.OpenPrice <= x.HighPrice &&
                           x.ClosePrice >= x.LowPrice && x.ClosePrice <= x.HighPrice)
                .WithMessage(OhlcRangeErrorMessage);
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        public override ValidationResult Validate(ValidationContext<VixFuturesEodDataReadModel> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.InstanceToValidate is null)
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("VixFuturesEodDataReadModel", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
