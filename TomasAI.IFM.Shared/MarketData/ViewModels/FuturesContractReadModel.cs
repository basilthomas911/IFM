using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketData.ViewModels;

/// <summary>
/// Represents a futures contract with detailed information, including contract identifiers, trading details, and
/// metadata.
/// </summary>
/// <remarks>This view model is used to encapsulate the key attributes of a futures contract, such as its unique
/// identifiers, trading symbol, security type, currency, exchange, and other relevant details. It also provides a
/// derived property to generate a composite identifier for the contract.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesContractV2ReadModel
{
    [Key(0)]
    public string ContractId { get; init; } 
    [Key(1)]
    public string Description { get; init; }
    [Key(2)]
    public string Symbol { get; init; }
    [Key(3)]
    public string LocalSymbol { get; init; }
    [Key(4)]
    public string SecurityType { get; init; }
    [Key(5)]
    public string Currency { get; init; }
    [Key(6)]
    public string Exchange { get; init; }
    [Key(7)]
    public string Multiplier { get; init; }
    [Key(8)]
    public DateOnly LastTradeDate { get; init; }
    [Key(9)]
    public bool CurrentlyTraded { get; init; }

    [JsonIgnore]
    [IgnoreMember]
    public FuturesContractId Id =>  new (ContractId, Symbol, LastTradeDate);

    public FuturesContractV2ReadModel() 
    {
        ContractId = default!;
        Symbol = default!;
        LastTradeDate = DateOnly.MinValue;
    }

    [SerializationConstructor]
    public FuturesContractV2ReadModel(
        string contractId,
        string description,
        string symbol,
        string localSymbol,
        string securityType,
        string currency,
        string exchange,
        string multiplier,
        DateOnly lastTradeDate,
        bool currentlyTraded)
    {
        ContractId = contractId;
        Description = description;
        Symbol = symbol;
        LocalSymbol = localSymbol;
        SecurityType = securityType;
        Currency = currency;
        Exchange = exchange;
        Multiplier = multiplier;
        LastTradeDate = lastTradeDate;
        CurrentlyTraded = currentlyTraded;
    }

    [IgnoreMember]
    public bool IsValid
        => !string.IsNullOrEmpty(ContractId ) && !string.IsNullOrEmpty(Symbol) && LastTradeDate > DateOnly.MinValue;
}

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesContractV2ReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates futures contract read model ensuring all required fields are present, valid, and consistent
/// with business rules for futures contracts.
/// </remarks>
public class FuturesContractValidationRules : BaseValidationRules, IValidationRules<FuturesContractV2ReadModel>
{
    /// <summary>
    /// Executes validation rules against the specified FuturesContractV2ReadModel instance.
    /// </summary>
    /// <param name="futuresContract">The futures contract read model to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(FuturesContractV2ReadModel futuresContract) 
        => Validate(futuresContract, new FuturesContractV2ReadModelValidator());

    /// <summary>
    /// Internal FluentValidation validator for FuturesContractV2ReadModel.
    /// </summary>
    private class FuturesContractV2ReadModelValidator : AbstractValidator<FuturesContractV2ReadModel>
    {
        public FuturesContractV2ReadModelValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage("FuturesContract.ContractId is required");

            // Symbol validation
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("FuturesContract.Symbol is required");

            // LocalSymbol validation
            RuleFor(x => x.LocalSymbol)
                .NotEmpty()
                .WithMessage("FuturesContract.LocalSymbol is required");

            // SecurityType validation - must be "FUT"
            RuleFor(x => x.SecurityType)
                .NotEmpty()
                .Equal("FUT")
                .WithMessage("FuturesContract.SecurityType must be 'FUT'");

            // Currency validation
            RuleFor(x => x.Currency)
                .NotEmpty()
                .WithMessage("FuturesContract.Currency is required");

            // Exchange validation
            RuleFor(x => x.Exchange)
                .NotEmpty()
                .WithMessage("FuturesContract.Exchange is required");

            // Multiplier validation
            RuleFor(x => x.Multiplier)
                .NotEmpty()
                .WithMessage("FuturesContract.Multiplier is required");

            // LastTradeDate validation - must not be min or max value
            RuleFor(x => x.LastTradeDate)
                .Must(lastTradeDate => lastTradeDate != DateOnly.MinValue && lastTradeDate != DateOnly.MaxValue)
                .WithMessage("FuturesContract.LastTradeDate is invalid");

            // Description validation (optional but if provided should not be whitespace only)
            RuleFor(x => x.Description)
                .Must(description => description == null || !string.IsNullOrWhiteSpace(description))
                .WithMessage("FuturesContract.Description must not be whitespace only");
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        public override ValidationResult Validate(ValidationContext<FuturesContractV2ReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesContract", "FuturesContract instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
