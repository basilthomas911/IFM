using MessagePack;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.Shared;

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
