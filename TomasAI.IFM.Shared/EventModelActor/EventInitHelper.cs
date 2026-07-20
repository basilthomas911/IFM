using System.Collections.Concurrent;
using System.Reflection;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides helper methods for setting init-only properties on event objects via reflection.
/// </summary>
/// <remarks>
/// Init-only property setters are enforced at compile time only. At the IL level they are regular setters
/// with a <c>modreq(IsExternalInit)</c> marker, so <see cref="PropertyInfo.SetValue(object?, object?)"/>
/// can invoke them at runtime. This helper caches the <see cref="PropertyInfo"/> lookups for performance.
/// </remarks>
public static class EventInitHelper
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> _cache = new();

    /// <summary>
    /// Sets the value of an init-only (or regular) property on the given object using cached reflection.
    /// </summary>
    public static void SetProperty<T>(object target, string propertyName, T value)
    {
        var key = (target.GetType(), propertyName);
        var prop = _cache.GetOrAdd(key, static k => k.Item1.GetProperty(k.Item2));
        prop?.SetValue(target, value);
    }
}
