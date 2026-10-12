using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Actor.Client;

/// <summary>
/// Simple test entity identifier for unit tests (formats as yyyy-MM-dd).
/// </summary>
[MessagePackObject]
public readonly record struct TestId : IActorEntityId
{
    /// <summary>As-of (value) date.</summary>
    [Key(0)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public TestId(DateOnly valueDate) => ValueDate = valueDate;

    /// <summary>
    /// Formats the identifier using a dotted representation. For this single-field id it returns yyyy-MM-dd.
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[48], $"{ValueDate:yyyy-MM-dd}");
}
