using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable view model describing a lookup type definition (name, short code, ordering, description, and creation metadata).
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived identifiers
/// are excluded via <see cref="IgnoreMemberAttribute"/> and <see cref="JsonIgnoreAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeReadModel
{
    /// <summary>Human-readable name of the lookup type.</summary>
    [Key(0)]
    public string LookupTypeName { get; init; }

    /// <summary>Short code or mnemonic representing the lookup type.</summary>
    [Key(1)]
    public string ShortCode { get; init; }

    /// <summary>Ordering/grouping identifier for presentation or processing.</summary>
    [Key(2)]
    public int OrderId { get; init; }

    /// <summary>Descriptive text providing context for the lookup type.</summary>
    [Key(3)]
    public string Description { get; init; }

    /// <summary>UTC timestamp when this lookup type definition was created.</summary>
    [Key(4)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or system that created this lookup type.</summary>
    [Key(5)]
    public string CreatedBy { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers; initializes string fields to empty and numeric fields to defaults.
    /// </summary>
    public LookupTypeReadModel()
    {
        LookupTypeName = string.Empty;
        ShortCode = string.Empty;
        OrderId = 0;
        Description = string.Empty;
        CreatedOn = DateTime.UtcNow;
        CreatedBy = string.Empty;
    }

    /// <summary>
    /// Creates a new lookup type view model instance.
    /// </summary>
    public LookupTypeReadModel(
        string lookupTypeName,
        string shortCode,
        int orderId,
        string description,
        DateTime createdOn,
        string createdBy)
    {
        LookupTypeName = lookupTypeName;
        ShortCode = shortCode;
        OrderId = orderId;
        Description = description;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }

    /// <summary>Derived identifier combining name and order (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public LookupTypeId Id => new(LookupTypeName, OrderId);

    /// <summary>Derived short code identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public LookupTypeShortCode ShortCodeId => new(LookupTypeName, ShortCode);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => !string.IsNullOrEmpty(LookupTypeName) && !string.IsNullOrEmpty(ShortCode);

    /// <summary>
    /// Returns a compact JSON representation of the lookup type.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);

    /// <summary>
    /// Provides a default (empty) lookup type view model instance.
    /// </summary>
    public static LookupTypeReadModel Default => new(
        lookupTypeName: string.Empty,
        shortCode: string.Empty,
        orderId: -1,
        description: string.Empty,
        createdOn: DateTime.UtcNow,
        createdBy: string.Empty);
}

public sealed class LookupTypeValidationRules : BaseValidationRules, IValidationRules<LookupTypeReadModel>
{
    static readonly LookupTypeValidator Validator = new();

    public ValidationError[] Execute(LookupTypeReadModel lookupType) => Validate(lookupType, Validator);

    sealed class LookupTypeValidator : AbstractValidator<LookupTypeReadModel>
    {
        public LookupTypeValidator()
        {
            RuleFor(x => x.LookupTypeName).NotEmpty().WithMessage("LookupType.LookupTypeName is empty");
            RuleFor(x => x.ShortCode).NotEmpty().WithMessage("LookupType.ShortCode is empty");
            RuleFor(x => x.OrderId).GreaterThanOrEqualTo(0).WithMessage("LookupType.OrderId must be non-negative");
            RuleFor(x => x.Description).NotNull().WithMessage("LookupType.Description is null");
            RuleFor(x => x.CreatedOn)
                .Must(value => value > DateTime.MinValue && value < DateTime.MaxValue)
                .WithMessage("LookupType.CreatedOn is invalid");
            RuleFor(x => x.CreatedBy).NotEmpty().WithMessage("LookupType.CreatedBy is empty");
        }

        public override ValidationResult Validate(ValidationContext<LookupTypeReadModel> context)
            => context.InstanceToValidate is null
                ? new ValidationResult([new ValidationFailure("LookupType", "LookupType instance is null")])
                : base.Validate(context);
    }
}

public static class LookupTypeReadModelValidationExtensions
{
    static readonly LookupTypeValidationRules Rules = new();

    public static List<ValidationError> ValidateLookupType(
        this List<ValidationError> validationErrors,
        LookupTypeReadModel? lookupType)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        validationErrors.AddRange(Rules.Execute(lookupType!));
        return validationErrors;
    }
}
