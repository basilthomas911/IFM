using System.Collections.Concurrent;
using MessagePack;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;

public delegate void DisplayValueWriter(ref MessagePackWriter writer, object value, bool camelCase, bool enumNames, int depth, bool contractKeys);

/// <summary>Compile-time generated accessors. Runtime discovery and reflection invocation are not used for encoding.</summary>
public sealed record DisplayProperty(string Name, string CamelName, Type PropertyType, Func<object, object?> Read, string DisplayName, bool IsBrowsable, DisplayValueWriter Write, string WireName);
public static class DisplayDescriptors
{
    public static void Include<T>() { }
    static readonly ConcurrentDictionary<Type, DisplayProperty[]> Shapes = new();
    static readonly ConcurrentDictionary<Type, Func<DisplayProperty[]>> Factories = new();
    public static IEnumerable<T> SequenceMarker<T>(T marker) => Array.Empty<T>();
    public static IReadOnlyList<T> ReadOnlyListMarker<T>(T marker) => Array.Empty<T>();
    public static List<T> ListMarker<T>(T marker) => [];
    public static Dictionary<TKey, TValue> DictionaryMarker<TKey, TValue>(TKey key, TValue value) where TKey : notnull => [];
    public static IReadOnlyDictionary<TKey, TValue> ReadOnlyDictionaryMarker<TKey, TValue>(TKey key, TValue value) where TKey : notnull => new Dictionary<TKey, TValue>();
    public static DisplayProperty Property<T, TValue>(T marker, string name, Func<T, TValue> read, string? displayName = null, bool browsable = true, string? wireName = null)
        => new(name, char.ToLowerInvariant(name[0]) + name[1..], typeof(TValue), value => read((T)value), displayName ?? name, browsable, (ref MessagePackWriter writer, object value, bool camelCase, bool enumNames, int depth, bool contractKeys) => BridgeDocument.WriteTyped(ref writer, read((T)value), camelCase, enumNames, depth, contractKeys), wireName ?? name);
    public static void Register<T>(T marker, params DisplayProperty[] properties) => Shapes.TryAdd(typeof(T), properties);
    public static void Register<T>(T marker, Func<DisplayProperty[]> factory) => Factories.TryAdd(typeof(T),factory);
    public static DisplayProperty[] For(Type type) => Shapes.TryGetValue(type, out var properties) ? properties :
        Factories.TryGetValue(type,out var factory) ? Shapes.GetOrAdd(type,static (_,create)=>create(),factory) :
        throw new NotSupportedException($"Display type {type} has no generated schema. Reference the Rust display generator in its declaring assembly.");
}
