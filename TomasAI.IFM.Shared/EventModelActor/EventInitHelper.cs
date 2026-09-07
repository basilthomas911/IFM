using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Sets init-only event properties using cached typed setters where possible.
/// </summary>
/// <remarks>
/// Init-only property setters are enforced at compile time only. At the IL level they are regular setters
/// with a <c>modreq(IsExternalInit)</c> marker, so <see cref="PropertyInfo.SetValue(object?, object?)"/>
/// can invoke them at runtime. Compiled setters avoid reflection invocation and value boxing on warm calls.
/// Reflection remains the fallback for conversions, boxed structs and non-public or unusual setters.
/// </remarks>
public static class EventInitHelper
{
    static class Setters<T>
    {
        internal static readonly ConcurrentDictionary<(Type, string), Action<object, T>> Cache = new();
    }

    /// <summary>
    /// Sets the value of an init-only (or regular) property on the given object using a cached setter.
    /// </summary>
    public static void SetProperty<T>(object target, string propertyName, T value)
        => Setters<T>.Cache.GetOrAdd(
            (target.GetType(), propertyName),
            static key => CreateSetter<T>(key.Item1, key.Item2))(target, value);

    static Action<object, T> CreateSetter<T>(Type type, string propertyName)
    {
        var property = type.GetProperty(propertyName);
        if (!RuntimeFeature.IsDynamicCodeSupported || type.IsValueType
            || property?.SetMethod is not { IsPublic: true, IsStatic: false } setter
            || property.PropertyType != typeof(T) || property.GetIndexParameters().Length != 0)
            return (target, value) => property?.SetValue(target, value);

        var target = Expression.Parameter(typeof(object));
        var value = Expression.Parameter(typeof(T));
        var assign = Expression.Lambda<Action<object, T>>(
            Expression.Call(Expression.Convert(target, type), setter, value), target, value).Compile();

        // Preserve PropertyInfo.SetValue's exception contract for exceptions thrown by the setter.
        return (target, value) =>
        {
            try
            {
                assign(target, value);
            }
            catch (Exception error)
            {
                throw new TargetInvocationException(error);
            }
        };
    }
}
