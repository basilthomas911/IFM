using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Represents the unique identifier for a futures end-of-day data snapshot composed of a contract identifier
/// and a value date.
/// </summary>
/// <remarks>
/// MessagePack serializable (only primitive components are serialized). Provides helper methods for creation,
/// formatting, and JSON representation. Use <see cref="Create(string, DateOnly)"/> for explicit construction
/// when clarity is desired.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesDataId
    : IActorEntityId
{
    /// <summary>
    /// Full contract identifier (root + contract month/year code).
    /// </summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>
    /// The value date (trading/settlement date) associated with the data.
    /// </summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for some serializers and tooling scenarios.
    /// </summary>
    public FuturesDataId() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesDataId"/> record.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    public FuturesDataId(string contractId, DateOnly valueDate)
    {
        ContractId = contractId;
        ValueDate = valueDate;
    }

    /// <summary>
    /// Factory method for creating a new <see cref="FuturesDataId"/> instance.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A new <see cref="FuturesDataId"/>.</returns>
    public static FuturesDataId Create(string contractId, DateOnly valueDate) => new(contractId, valueDate);

    /// <summary>
    /// Formats the identifier into a stable string key representation.
    /// </summary>
    /// <returns>Formatted string (ContractId.ValueDate in yyyy-MM-dd).</returns>
    public string Format() => string.Create(null, stackalloc char[64], $"{ContractId}.{ValueDate:yyyy-MM-dd}");

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [IgnoreMember]
    public bool IsValid
        => !string.IsNullOrWhiteSpace(ContractId) && ValueDate > DateOnly.MinValue;
}

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesDataId"/> instances.
/// </summary>
/// <remarks>
/// Validates futures data identifier ensuring all required fields are present and valid.
/// </remarks>
public class FuturesDataIdValidationRules : BaseValidationRules, IValidationRules<FuturesDataId>
{
    public const string InstanceErrorMessage = "FuturesDataId instance is null";
    public const string ContractIdErrorMessage = "FuturesDataId.ContractId is required";
    public const string ValueDateErrorMessage = "FuturesDataId.ValueDate is invalid";

    /// <summary>
    /// Executes validation rules against the specified FuturesDataId instance.
    /// </summary>
    /// <param name="futuresDataId">The futures data identifier to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(FuturesDataId futuresDataId) 
        => Validate(futuresDataId, new FuturesDataIdValidator());

    /// <summary>
    /// Internal FluentValidation validator for FuturesDataId.
    /// </summary>
    class FuturesDataIdValidator : AbstractValidator<FuturesDataId>
    {
        public FuturesDataIdValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage(ContractIdErrorMessage);

            // ValueDate validation - must not be min or max value
            RuleFor(x => x.ValueDate)
                .Must(valueDate => valueDate != DateOnly.MinValue && valueDate != DateOnly.MaxValue)
                .WithMessage(ValueDateErrorMessage);
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        public override ValidationResult Validate(ValidationContext<FuturesDataId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesDataId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
