using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Validation;

public class OptionTradeValidationRules : BaseValidationRules, IValidationRules<OptionTradeReadModel>
{
    static readonly OptionTradeValidator Validator = new();
    static readonly OptionLegValidator OptionLegRule = new();
    static readonly TradePositionValidator TradePositionRule = new();
    static readonly TradeLimitValidator TradeLimitRule = new();
    static readonly TradeTypeLimitValidator TradeTypeLimitRule = new();
    static readonly TradeFillValidator TradeFillRule = new();

    public const string InstanceErrorMessage = "OptionTrade instance is null";
    public const string TradeDateErrorMessage = "OptionTrade.TradeDate is invalid";
    public const string MaturityDateErrorMessage = "OptionTrade.MaturityDate is invalid";
    public const string UnderlyingContractIdErrorMessage = "OptionTrade.UnderlyingContractId is empty";

    public OptionTradeValidationRules OptionTrade => this;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    public OptionLegValidator OptionLeg => OptionLegRule;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    public TradePositionValidator TradePosition => TradePositionRule;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    public TradeLimitValidator TradeLimit => TradeLimitRule;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    public TradeTypeLimitValidator TradeTypeLimits => TradeTypeLimitRule;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserve the existing public instance model API used by consumers and reflection.")]
    public TradeFillValidator TradeFills => TradeFillRule;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the related validation rules together so the full contract is checked in the established order.")]
    public ValidationError[] Execute(OptionTradeReadModel optionTrade)
    {
        ArgumentNullException.ThrowIfNull(optionTrade);
        var validationErrors = new List<ValidationError>();

        // validate option trade...
        var ruleErrors = Validate(optionTrade, Validator);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);

        // validate option legs...
        if (optionTrade.OptionLegs is not null)
        {
            foreach (var optionLeg in optionTrade.OptionLegs)
            {
                ruleErrors = Validate(optionLeg, OptionLeg);
                if (ruleErrors is not null)
                    validationErrors.AddRange(ruleErrors);
            }
        }

        // validate trade positions...
        if (optionTrade.TradePositions is not null)
        {
            foreach (var tradePosition in optionTrade.TradePositions)
            {
                ruleErrors = Validate(tradePosition, TradePosition);
                if (ruleErrors is not null)
                    validationErrors.AddRange(ruleErrors);
            }
        }

        // validate trade limits...
        ruleErrors = Validate(optionTrade.TradeLimit, TradeLimit!);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);

        // validate trade type limits...
        if (optionTrade.TradeTypeLimits is not null)
        {
            foreach (var tradeTypeLimit in optionTrade.TradeTypeLimits)
            {
                ruleErrors = Validate(tradeTypeLimit, TradeTypeLimits);
                if (ruleErrors is not null)
                    validationErrors.AddRange(ruleErrors);

            }
        }

        // validate trade fills...
        if (optionTrade.TradeFills is not null)
        {
            foreach (var tradeFill in optionTrade.TradeFills)
            {
                ruleErrors = Validate(tradeFill, TradeFills);
                if (ruleErrors is not null)
                    validationErrors.AddRange(ruleErrors);
            }
        }

        // validate that all valid trade child objects have same trade id
        if (validationErrors.Count == 0)
        {
            foreach (var childTradeId in optionTrade.OptionLegs!.Select(child => child.TradeId).Where(id => id != optionTrade.TradeId))
                    validationErrors.Add(new ValidationError($"OptionLeg.TradeId: {childTradeId} != {optionTrade.TradeId}"));

            foreach (var childTradeId in optionTrade.TradePositions!.Select(child => child.TradeId).Where(id => id != optionTrade.TradeId))
                    validationErrors.Add(new ValidationError($"TradePosition.TradeId: {childTradeId} != {optionTrade.TradeId}"));

            if (optionTrade.TradeLimit!.TradeId != optionTrade.TradeId)
                validationErrors.Add(new ValidationError($"TradeLimit.TradeId: {optionTrade.TradeLimit.TradeId} != {optionTrade.TradeId}"));

            foreach (var childTradeId in optionTrade.TradeTypeLimits!.Select(child => child.TradeId).Where(id => id != optionTrade.TradeId))
                    validationErrors.Add(new ValidationError($"TradeTypeLimit.TradeId: {childTradeId} != {optionTrade.TradeId}"));

            foreach (var childTradeId in optionTrade.TradeFills!.Select(child => child.TradeId).Where(id => id != optionTrade.TradeId))
                    validationErrors.Add(new ValidationError($"TradeFill.TradeId: {childTradeId} != {optionTrade.TradeId}"));
        }
        return [.. validationErrors];
    }

    private sealed class OptionTradeValidator : AbstractValidator<OptionTradeReadModel>
    {
        public OptionTradeValidator()
        {
            RuleFor(x => x.TradeDate).Must(e => e != DateOnly.MinValue && e != DateOnly.MaxValue).WithMessage(TradeDateErrorMessage);
            RuleFor(x => x.MaturityDate).Must(e => e != DateOnly.MinValue && e != DateOnly.MaxValue).WithMessage(MaturityDateErrorMessage);
            RuleFor(x => x.UnderlyingContractId).NotEmpty().WithMessage(UnderlyingContractIdErrorMessage);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Convert malformed model exceptions into the existing aggregated validation failures.")]
        public override ValidationResult Validate(ValidationContext<OptionTradeReadModel> context)
        {
            try
            {
                if (context.InstanceToValidate is null)
                    throw new ArgumentNullException(nameof(context));
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("OptionTrade", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "Preserve the existing public nested model identity used by consumers.")]
    public sealed class OptionLegValidator : AbstractValidator<OptionTradeLegReadModel>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3218", Justification = "Preserve the existing public nested validation contract or required JsonConverter override member name.")]
        public const string InstanceErrorMessage = "OptionLeg instance is null";
        public const string TradeIdErrorMessage = "OptionLeg.TradeId must be > 0";
        public const string ContractIdErrorMessage = "OptionLeg.ContractId is empty";
        public const string QuantityErrorMessage = "OptionLeg.Quantity must be > 0";

        public OptionLegValidator()
        {
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeDateErrorMessage);
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage(QuantityErrorMessage);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Convert malformed model exceptions into the existing aggregated validation failures.")]
        public override ValidationResult Validate(ValidationContext<OptionTradeLegReadModel> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            try
            {
                if (context.InstanceToValidate is null)
                    throw new ArgumentNullException(nameof(context));
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("OptionLeg", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "Preserve the existing public nested model identity used by consumers.")]
    public sealed class TradePositionValidator : AbstractValidator<TradePositionReadModel>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3218", Justification = "Preserve the existing public nested validation contract or required JsonConverter override member name.")]
        public const string InstanceErrorMessage = "TradePosition instance is null";
        public const string OrderIdErrorMessage = "TradePosition.OrderId must be > 0";
        public const string TradeIdErrorMessage = "TradePosition.TradeId must be > 0";
        public const string ValueDateErrorMessage = "TradePosition.ValueDate is invalid";
        public const string DaysToExpiryErrorMessage = "TradePosition.Quantity must be positive";

        public TradePositionValidator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage(OrderIdErrorMessage);
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.ValueDate).Must(e => e != DateOnly.MinValue && e != DateOnly.MaxValue).WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.DaysToExpiry).GreaterThanOrEqualTo(0).WithMessage(DaysToExpiryErrorMessage);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Convert malformed model exceptions into the existing aggregated validation failures.")]
        public override ValidationResult Validate(ValidationContext<TradePositionReadModel> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            try
            {
                if (context.InstanceToValidate is null)
                    throw new ArgumentNullException(nameof(context));
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("TradePosition", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "Preserve the existing public nested model identity used by consumers.")]
    public sealed class TradeLimitValidator : AbstractValidator<TradeLimitReadModel>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3218", Justification = "Preserve the existing public nested validation contract or required JsonConverter override member name.")]
        public const string InstanceErrorMessage = "TradeLimit instance is null";
        public const string TradeIdErrorMessage = "TradeLimit.TradeId must be > 0";

        public TradeLimitValidator()
        {
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Convert malformed model exceptions into the existing aggregated validation failures.")]
        public override ValidationResult Validate(ValidationContext<TradeLimitReadModel> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            try
            {
                if (context.InstanceToValidate is null)
                    throw new ArgumentNullException(nameof(context));
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("TradeLimit", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "Preserve the existing public nested model identity used by consumers.")]
    public sealed class TradeTypeLimitValidator : AbstractValidator<TradeTypeLimitReadModel>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3218", Justification = "Preserve the existing public nested validation contract or required JsonConverter override member name.")]
        public const string InstanceErrorMessage = "TradeTypeLimit instance is null";
        public const string TradeIdErrorMessage = "TradeTypeLimit.TradeId must be > 0";

        public TradeTypeLimitValidator()
        {
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Convert malformed model exceptions into the existing aggregated validation failures.")]
        public override ValidationResult Validate(ValidationContext<TradeTypeLimitReadModel> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            try
            {
                if (context.InstanceToValidate is null)
                    throw new ArgumentNullException(nameof(context));
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("TradeTypeLimit", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "Preserve the existing public nested model identity used by consumers.")]
    public sealed class TradeFillValidator : AbstractValidator<TradeFillReadModel>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3218", Justification = "Preserve the existing public nested validation contract or required JsonConverter override member name.")]
        public const string InstanceErrorMessage = "TradeTypeLimit instance is null";
        public const string TradeIdErrorMessage = "TradeTypeLimit.TradeId must be > 0";

        public TradeFillValidator()
        {
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Convert malformed model exceptions into the existing aggregated validation failures.")]
        public override ValidationResult Validate(ValidationContext<TradeFillReadModel> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            try
            {
                if (context.InstanceToValidate is null)
                    throw new ArgumentNullException(nameof(context));
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("TradeFill", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
