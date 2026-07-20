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

/// <summary>
/// Represents a scalar value of a specified value type, providing convenient conversions to common numeric types.
/// </summary>
/// <remarks>This class is useful for working with generic numeric values where the underlying type is not known
/// at compile time. The conversion properties assume that the underlying type supports conversion to double and int;
/// otherwise, a runtime exception may occur.</remarks>
/// <typeparam name="TScalar">The value type of the scalar. Must be a struct that can be converted to numeric types.</typeparam>
[MessagePackObject]
public class ScalarValue<TScalar>(TScalar value) 
    where TScalar : struct
{
    [Key(0)]
    public TScalar Value { get; set; } = value;
    
    [IgnoreMember]
    public double AsDouble => Convert.ToDouble(Value);
    
    [IgnoreMember]
    public int AsInteger => Convert.ToInt32(Value);
}
