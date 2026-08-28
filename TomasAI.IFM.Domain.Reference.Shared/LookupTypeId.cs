using MessagePack;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.Shared;

/// <summary>
/// MessagePack-serializable identifier for a lookup type composed of a name and an order id.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation: "LookupTypeName.OrderId".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeId(
    /// <summary>The descriptive name of the lookup type.</summary>
    [property: Key(0)] string LookupTypeName,
    /// <summary>The ordering or grouping identifier for the lookup type.</summary>
    [property: Key(1)] int OrderId) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public LookupTypeId() : this(string.Empty, 0) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "LookupTypeName.OrderId".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{LookupTypeName}.{OrderId}");

    /// <summary>
    /// Returns a human-readable string representation of the identifier.
    /// </summary>
    public override string ToString() => $"{LookupTypeName ?? string.Empty} : {OrderId}";
}

public sealed class LookupTypeIdValidationRules : BaseValidationRules, IValidationRules<LookupTypeId>
{
    static readonly LookupTypeIdValidator Validator = new();

    public ValidationError[] Execute(LookupTypeId lookupTypeId) => Validate(lookupTypeId, Validator);

    sealed class LookupTypeIdValidator : AbstractValidator<LookupTypeId>
    {
        public LookupTypeIdValidator()
        {
            RuleFor(x => x.LookupTypeName).NotEmpty().WithMessage("LookupTypeId.LookupTypeName is empty");
            RuleFor(x => x.OrderId).GreaterThanOrEqualTo(0).WithMessage("LookupTypeId.OrderId must be non-negative");
        }

        public override ValidationResult Validate(ValidationContext<LookupTypeId> context)
            => context.InstanceToValidate is null
                ? new ValidationResult([new ValidationFailure("LookupTypeId", "LookupTypeId instance is null")])
                : base.Validate(context);
    }
}

public static class LookupTypeIdValidationExtensions
{
    static readonly LookupTypeIdValidationRules Rules = new();

    public static List<ValidationError> ValidateLookupTypeId(
        this List<ValidationError> validationErrors,
        LookupTypeId? lookupTypeId,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        validationErrors.AddRange(Rules.Execute(lookupTypeId!));
        return validationErrors;
    }
}

/// <summary>
/// MessagePack-serializable identifier pairing a lookup type name with a short code.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation: "LookupTypeName.ShortCode".
/// </remarks>
/// <param name="LookupTypeName">The descriptive name of the lookup type.</param>
/// <param name="ShortCode">The short code associated with the lookup type.</param>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeShortCode(
    [property: Key(0)] string LookupTypeName,
    [property: Key(1)] string ShortCode) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to empty strings.
    /// </summary>
    public LookupTypeShortCode() : this(string.Empty, string.Empty) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "LookupTypeName.ShortCode".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{LookupTypeName}.{ShortCode}");

    /// <summary>
    /// Returns a human-readable string representation of the identifier.
    /// </summary>
    public override string ToString() => $"{LookupTypeName ?? string.Empty} : {ShortCode ?? string.Empty}";
}
