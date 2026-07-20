using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Validation;

/// <summary>
/// Provides validation rules for instances of the FuturesOptionQuoteReadModel to ensure required fields are present and
/// meet defined criteria.
/// </summary>
/// <remarks>This class implements the IValidationRules interface for FuturesOptionQuoteReadModel and extends
/// BaseValidationRules. It validates that key properties such as RequestId, QuoteId, and ContractId are populated and
/// conform to expected constraints. Use this class to perform validation before processing or persisting
/// FuturesOptionQuoteReadModel data.</remarks>
public class FuturesOptionQuoteValidationRules : BaseValidationRules, IValidationRules<FuturesOptionQuoteReadModel>
{
    public const string InstanceErrorMessage = "FuturesOptionQuote instance is null";
    public const string StreamIdErrorMessage = "FuturesOptionQuote.StreamId is required";
    public const string ContractIdErrorMessage = "FuturesOptionQuote.ContractId is required";
    public const string RequestIdErrorMessage = "FuturesOptionQuote.Request must be greater than zero";
    public const string QuoteIdErrorMessage = "FuturesOptionQuote.QuoteId is required";

     public ValidationError[] Execute(FuturesOptionQuoteReadModel futuresOptionQuote) => Validate(futuresOptionQuote, new FuturesOptionQuoteValidator() );

    class FuturesOptionQuoteValidator : AbstractValidator<FuturesOptionQuoteReadModel>
    {
        public FuturesOptionQuoteValidator()
        {
            RuleFor(x => x.RequestId).GreaterThanOrEqualTo(1).WithMessage(RequestIdErrorMessage);
            RuleFor(x => x.QuoteId).NotEmpty().WithMessage(QuoteIdErrorMessage);
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<FuturesOptionQuoteReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate, nameof(context.InstanceToValidate));
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesOptionQuote", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
