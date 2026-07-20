using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Validation;

public class OptionTradeValidationRules : BaseValidationRules, IValidationRules<OptionTradeReadModel>
{
    readonly OptionLegValidator _optionLeg = new OptionLegValidator();
    readonly TradePositionValidator _tradePosition = new TradePositionValidator();
    readonly TradeLimitValidator _tradeLimit = new TradeLimitValidator();
    readonly TradeTypeLimitValidator _tradeTypeLimit = new TradeTypeLimitValidator();
    readonly TradeFillValidator _tradeFill = new TradeFillValidator();

    public const string InstanceErrorMessage = "OptionTrade instance is null";
    public const string TradeDateErrorMessage = "OptionTrade.TradeDate is invalid";
    public const string MaturityDateErrorMessage = "OptionTrade.MaturityDate is invalid";
    public const string UnderlyingContractIdErrorMessage = "OptionTrade.UnderlyingContractId is empty";

    public OptionTradeValidationRules OptionTrade => this;
    public OptionLegValidator OptionLeg => _optionLeg;
    public TradePositionValidator TradePosition => _tradePosition;
    public TradeLimitValidator TradeLimit => _tradeLimit;
    public TradeTypeLimitValidator TradeTypeLimits => _tradeTypeLimit;
    public TradeFillValidator TradeFills => _tradeFill;

    public ValidationError[] Execute(OptionTradeReadModel optionTrade)
    {
        var validationErrors = new List<ValidationError>();

        // validate option trade...
        var ruleErrors = Validate(optionTrade, new OptionTradeValidator());
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);

        // validate option legs...
        if (optionTrade.OptionLegs is not null)
        {
            foreach(var optionLeg in optionTrade.OptionLegs)
            {
                ruleErrors = Validate(optionLeg, OptionLeg);
                if (ruleErrors is not null)
                    validationErrors.AddRange(ruleErrors);
            }
        }

        // validate trade positions...
        if (optionTrade.TradePositions is not null)
        {
            foreach(var tradePosition in optionTrade.TradePositions)
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
            foreach (var optionLeg in optionTrade.OptionLegs!)
                if (optionLeg.TradeId != optionTrade.TradeId)
                    validationErrors.Add(new ValidationError($"OptionLeg.TradeId: {optionLeg.TradeId} != {optionTrade.TradeId}") );

            foreach (var tradePosition in optionTrade.TradePositions!)
                if (tradePosition.TradeId != optionTrade.TradeId)
                    validationErrors.Add(new ValidationError($"TradePosition.TradeId: {tradePosition.TradeId} != {optionTrade.TradeId}"));

            foreach (var tradePosition in optionTrade.TradePositions)
                if (optionTrade?.TradeLimit!.TradeId != optionTrade?.TradeId)
                    validationErrors.Add(new ValidationError($"TradeLimit.TradeId: {optionTrade?.TradeLimit!.TradeId} != {optionTrade?.TradeId}"));

            foreach (var tradeTypeLimit in optionTrade?.TradeTypeLimits!)
                if (tradeTypeLimit.TradeId != optionTrade.TradeId)
                    validationErrors.Add(new ValidationError($"TradeTypeLimit.TradeId: {tradeTypeLimit.TradeId} != {optionTrade.TradeId}"));

            foreach (var tradeFill in optionTrade.TradeFills!)
                if (tradeFill.TradeId != optionTrade.TradeId)
                    validationErrors.Add(new ValidationError($"TradeFill.TradeId: {tradeFill.TradeId} != {optionTrade.TradeId}"));
        }
        return [.. validationErrors];
    }

    private class OptionTradeValidator : AbstractValidator<OptionTradeReadModel>
    {
        public OptionTradeValidator()
        {
            RuleFor(x => x.TradeDate).Must(e => e != DateOnly.MinValue && e != DateOnly.MaxValue).WithMessage(TradeDateErrorMessage);
            RuleFor(x => x.MaturityDate).Must(e => e != DateOnly.MinValue && e != DateOnly.MaxValue).WithMessage(MaturityDateErrorMessage);
            RuleFor(x => x.UnderlyingContractId).NotEmpty().WithMessage(UnderlyingContractIdErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<OptionTradeReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
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

    public class OptionLegValidator : AbstractValidator<OptionTradeLegReadModel>
    {
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

        public override ValidationResult Validate(ValidationContext<OptionTradeLegReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
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

    public class TradePositionValidator : AbstractValidator<TradePositionReadModel>
    {
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

        public override ValidationResult Validate(ValidationContext<TradePositionReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);  
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

    public class TradeLimitValidator : AbstractValidator<TradeLimitReadModel>
    {
        public const string InstanceErrorMessage = "TradeLimit instance is null";
        public const string TradeIdErrorMessage = "TradeLimit.TradeId must be > 0";

        public TradeLimitValidator()
        {
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<TradeLimitReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
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

    public class TradeTypeLimitValidator : AbstractValidator<TradeTypeLimitReadModel>
    {
        public const string InstanceErrorMessage = "TradeTypeLimit instance is null";
        public const string TradeIdErrorMessage = "TradeTypeLimit.TradeId must be > 0";

        public TradeTypeLimitValidator()
        {
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<TradeTypeLimitReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
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

    public class TradeFillValidator : AbstractValidator<TradeFillReadModel>
    {
        public const string InstanceErrorMessage = "TradeTypeLimit instance is null";
        public const string TradeIdErrorMessage = "TradeTypeLimit.TradeId must be > 0";

        public TradeFillValidator()
        {
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<TradeFillReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
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
