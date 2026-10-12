using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>
/// Validation rules for <see cref="FuturesRsiSignalReadModel"/>.
/// </summary>
public class FuturesRsiSignalReadModelValidationRules : BaseValidationRules, IValidationRules<FuturesRsiSignalReadModel>
{
    static readonly FuturesRsiSignalValidator Validator = new();

    public ValidationError[] Execute(FuturesRsiSignalReadModel futuresRsiSignal) => Validate(futuresRsiSignal, Validator);

    private sealed class FuturesRsiSignalValidator : AbstractValidator<FuturesRsiSignalReadModel>
    {
        public FuturesRsiSignalValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage("FuturesRsiSignal.ContractId is required");
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MinValue).WithMessage("FuturesRsiSignal.ValueDate must be greater than DateOnly.MinValue");
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MaxValue).WithMessage("FuturesRsiSignal.ValueDate must be less than DateOnly.MaxValue");
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MinValue).WithMessage("FuturesRsiSignal.Timestamp must be greater than TimeOnly.MinValue");
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MaxValue).WithMessage("FuturesRsiSignal.Timestamp must be less than TimeOnly.MaxValue");
            RuleFor(x => x.RS).Must(rs => !double.IsNaN(rs)).WithMessage("FuturesRsiSignal.RS is NaN");
            RuleFor(x => x.RSI).Must(rsi => !double.IsNaN(rsi)).WithMessage("FuturesRsiSignal.RSI is NaN");
            RuleFor(x => x.RSIAverage).Must(rsiAverage => !double.IsNaN(rsiAverage)).WithMessage("FuturesRsiSignal.RSIAverage is NaN");
            RuleFor(x => x.RSISlope).Must(rsiSlope => !double.IsNaN(rsiSlope)).WithMessage("FuturesRsiSignal.RSISlope is NaN");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Return aggregated validation failures for malformed external models rather than allowing exceptions to escape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The validation diagnostic intentionally identifies the validated model property.")]
        public override ValidationResult Validate(ValidationContext<FuturesRsiSignalReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesRsiSignal", "FuturesRsiSignal instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
