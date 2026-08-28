using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// MessagePack-serializable identifier for a fund, composed of a single integer value.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot-separated components; with a single
/// component it resolves to "Id".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundId(
    /// <summary>The unique numeric identifier of the fund.</summary>
    [property: Key(0)] int Id) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to zero.
    /// </summary>
    public FundId() : this(0) { }

    /// <summary>
    /// Formats the identifier as a dot-separated key.
    /// </summary>
    public string Format() => Id.ToString();

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

/// <summary>
/// Intrinsic validation for the Fund aggregate identifier.
/// </summary>
public static class FundIdValidationExtensions
{
    public static List<ValidationError> ValidateFundId(
        this List<ValidationError> validationErrors,
        FundId? fundId,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        if (fundId is null)
            validationErrors.Add(new($"{commandName}.EntityId is null"));
        else if (fundId.Id < 1)
            validationErrors.Add(new($"{commandName}.EntityId.Id must be > 0"));

        return validationErrors;
    }
}
