using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Validation rules for <see cref="FuturesMacdSignalId"/>.
/// </summary>
public class FuturesMacdSignalIdValidationRules : BaseValidationRules, IValidationRules<FuturesMacdSignalId>
{
    public const string InstanceErrorMessage = "FuturesMacdSignalId instance is null";
    public const string ContractIdErrorMessage = "FuturesMacdSignalId: ContractId is required";
    public const string ValueDateMinErrorMessage = "FuturesMacdSignalId: ValueDate must be greater than DateOnly.MinValue";
    public const string ValueDateMaxErrorMessage = "FuturesMacdSignalId: ValueDate must be less than DateOnly.MaxValue";
    public const string TimestampMinErrorMessage = "FuturesMacdSignalId: Timestamp must be greater than TimeOnly.MinValue";
    public const string TimestampMaxErrorMessage = "FuturesMacdSignalId: Timestamp must be less than TimeOnly.MaxValue";

    static readonly FuturesMacdSignalIdValidator Validator = new();

    public ValidationError[] Execute(FuturesMacdSignalId macdSignalId) => Validate(macdSignalId, Validator);

    sealed class FuturesMacdSignalIdValidator : AbstractValidator<FuturesMacdSignalId>
    {
        public FuturesMacdSignalIdValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MinValue).WithMessage(ValueDateMinErrorMessage);
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MaxValue).WithMessage(ValueDateMaxErrorMessage);
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MinValue).WithMessage(TimestampMinErrorMessage);
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MaxValue).WithMessage(TimestampMaxErrorMessage);
            RuleFor(x => x.SignalEmaPeriod).GreaterThan(0);
            RuleFor(x => x.FastEmaPeriod).GreaterThan(0);
            RuleFor(x => x.SlowEmaPeriod).GreaterThan(0);
            RuleFor(x => x).Must(x => x.FastEmaPeriod < x.SlowEmaPeriod)
                .WithMessage("FuturesMacdSignalId: FastEmaPeriod must be less than SlowEmaPeriod");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Return aggregated validation failures for malformed external models rather than allowing exceptions to escape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The validation diagnostic intentionally identifies the validated model property.")]
        public override ValidationResult Validate(ValidationContext<FuturesMacdSignalId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesMacdSignalId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}


