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
