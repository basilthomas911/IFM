using MessagePack;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// Represents a view model that encapsulates a scalar value of a value type.
/// </summary>
/// <typeparam name="TScalar">The type of the scalar value. Must be a value type.</typeparam>
[MessagePackObject]
public class ScalarReadModel<TScalar>(TScalar value)
    where TScalar : struct
{
    [Key(0)]
    public TScalar Value { get; set; } = value;
}
