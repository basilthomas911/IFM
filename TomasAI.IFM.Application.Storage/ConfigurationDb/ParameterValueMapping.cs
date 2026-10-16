using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>Maps the closed parameter contract to typed relational rows. No encoded document is persisted.</summary>
internal static class ParameterValueMapping
{
    internal sealed record Column(string Name, Type Type, PropertyInfo? Property, bool Presence = false);
    internal sealed record Child(PropertyInfo Property, Table Table, bool Collection);
    internal sealed class Table(string name, Type type, Table? parent, bool scalar)
    {
        public string Name { get; } = name;
        public Type Type { get; } = type;
        public Table? Parent { get; } = parent;
        public bool Scalar { get; } = scalar;
        public List<Column> Columns { get; } = [];
        public List<Child> Children { get; } = [];
    }
    internal sealed record Row(Table Table, string Path, string? ParentPath, int Ordinal, object?[] Values);
    static readonly Dictionary<string, Table> Roots = BuildRoots();
    internal static IEnumerable<Table> Tables => Roots.Values.SelectMany(Descendants);
    internal static IEnumerable<Table> TablesFor(string component) => Descendants(Root(component));
    internal static Table Root(string component) => Roots.TryGetValue(component, out var table)
        ? table : throw new ArgumentException("Unknown parameter component", nameof(component));

    static Dictionary<string, Table> BuildRoots()
    {
        var variants = typeof(ParameterSetValue).GetCustomAttributes<UnionAttribute>();
        return variants.Select(x => x.SubType).ToDictionary(type =>
        {
            var value = Activator.CreateInstance(type.GetProperty("Value")!.PropertyType)!;
            return ((ParameterSetValue)Activator.CreateInstance(type, value)!).ComponentCode;
        }, type => Build(type.GetProperty("Value")!.PropertyType, null, "parameter_" + Snake(type.Name.Replace("ParameterValue", "", StringComparison.Ordinal))));
    }
    internal static Table Build(Type type, Table? parent, string name)
    {
        var table = new Table(Identifier(name), type, parent, IsScalar(type));
        if (table.Scalar) { table.Columns.Add(new("value", type, null)); return table; }
        foreach (var property in type.GetProperties().Where(x => x.GetCustomAttribute<KeyAttribute>() is not null)
                     .OrderBy(x => x.GetCustomAttribute<KeyAttribute>()!.IntKey))
        {
            if (IsScalar(property.PropertyType)) table.Columns.Add(new(Snake(property.Name), property.PropertyType, property));
            else
            {
                var element = Element(property.PropertyType);
                table.Columns.Add(new("has_" + Snake(property.Name), typeof(bool), property, true));
                table.Children.Add(new(property, Build(element ?? property.PropertyType, table, name + "_" + Snake(property.Name)), element is not null));
            }
        }
        return table;
    }
    internal static IEnumerable<Table> Descendants(Table table)
    {
        yield return table;
        foreach (var child in table.Children.SelectMany(x => Descendants(x.Table))) yield return child;
    }
    static Type? Element(Type type) => type.IsArray ? type.GetElementType() :
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>) ? type.GetGenericArguments()[0] : null;
    static bool IsPresent(object? value) => value is not null &&
        !(value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(ImmutableArray<>) &&
          (bool)value.GetType().GetProperty("IsDefault")!.GetValue(value)!);
    static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsEnum || type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(Guid) || type == typeof(DateOnly) || type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) || type == typeof(TimeSpan);
    }
    static string Snake(string name) => Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    static string Identifier(string name) => name.Length <= 63 ? name : name[..50] + "_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..12].ToLowerInvariant();
    internal static string SqlType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsEnum || type == typeof(int) || type == typeof(short) || type == typeof(byte) || type == typeof(ushort)) return "integer";
        if (type == typeof(long) || type == typeof(uint)) return "bigint";
        if (type == typeof(decimal) || type == typeof(ulong)) return "numeric";
        if (type == typeof(double)) return "double precision";
        if (type == typeof(float)) return "real";
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(Guid)) return "uuid";
        if (type == typeof(DateOnly)) return "date";
        if (type == typeof(DateTime)) return "bigint";
        if (type == typeof(DateTimeOffset)) return "text";
        if (type == typeof(TimeSpan)) return "interval";
        if (type == typeof(string)) return "text";
        throw new NotSupportedException(type.FullName);
    }
    internal static object? DatabaseValue(object? value, Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (value is null) return null;
        if (type.IsEnum || type == typeof(short) || type == typeof(byte) || type == typeof(ushort)) return Convert.ToInt32(value);
        if (type == typeof(uint)) return Convert.ToInt64(value);
        if (type == typeof(ulong)) return Convert.ToDecimal(value);
        if (value is DateTime date) return date.ToBinary();
        if (value is DateTimeOffset offset) return offset.ToString("O",System.Globalization.CultureInfo.InvariantCulture);
        return value;
    }
    internal static IReadOnlyList<Row> Write(ParameterSetValue value)
    {
        var rows = new List<Row>();
        var instance = value.GetType().GetProperty("Value")!.GetValue(value)!;
        Walk(Root(value.ComponentCode), instance, "$", null, 0, rows);
        return rows;
    }
    internal static void Walk(Table table, object value, string path, string? parent, int ordinal, List<Row> rows)
    {
        rows.Add(new(table, path, parent, ordinal, table.Columns.Select(column => column.Presence
            ? (object)IsPresent(column.Property!.GetValue(value))
            : DatabaseValue(column.Property is null ? value : column.Property.GetValue(value), column.Type)).ToArray()));
        foreach (var child in table.Children)
        {
            var nested = child.Property.GetValue(value);
            if (!IsPresent(nested)) continue;
            var childPath = path + "/" + child.Property.Name;
            if (child.Collection)
            {
                var index = 0;
                foreach (var item in (IEnumerable)nested)
                {
                    if (item is null) throw new ArgumentException("Null collection item at " + childPath);
                    Walk(child.Table, item, childPath + "/" + index, path, index++, rows);
                }
            }
            else Walk(child.Table, nested, childPath, path, 0, rows);
        }
    }
    internal static ParameterSetValue Read(string component, IReadOnlyList<Row> rows)
    {
        var root = Root(component);
        var value = Materialize(root, rows.Single(x => x.Table == root && x.Path == "$"), rows);
        var variant = typeof(ParameterSetValue).GetCustomAttributes<UnionAttribute>().Select(x => x.SubType)
            .Single(x => x.GetProperty("Value")!.PropertyType == root.Type);
        return (ParameterSetValue)Activator.CreateInstance(variant, value)!;
    }
    internal static object Materialize(Table table, Row row, IReadOnlyList<Row> rows)
    {
        if (table.Scalar) return ConvertValue(row.Values[0], table.Type)!;
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < table.Columns.Count; i++)
            if (!table.Columns[i].Presence) fields[table.Columns[i].Property!.Name] = ConvertValue(row.Values[i], table.Columns[i].Type);
        foreach (var child in table.Children)
        {
            var present = (bool)row.Values[table.Columns.FindIndex(x => x.Presence && x.Property == child.Property)]!;
            var children = rows.Where(x => x.Table == child.Table && x.ParentPath == row.Path).OrderBy(x => x.Ordinal).ToArray();
            if (!present)
            {
                if (children.Length != 0) throw new InvalidOperationException("Rows exist for absent child " + child.Property.Name);
                fields[child.Property.Name] = null; continue;
            }
            if (!child.Collection) fields[child.Property.Name] = Materialize(child.Table, children.Single(), rows);
            else
            {
                var array = Array.CreateInstance(child.Table.Type, children.Length);
                for (var i = 0; i < children.Length; i++)
                {
                    if (children[i].Ordinal != i) throw new InvalidOperationException("Noncontiguous parameter child ordinals");
                    array.SetValue(Materialize(child.Table, children[i], rows), i);
                }
                fields[child.Property.Name] = child.Property.PropertyType.IsArray ? array :
                    typeof(ParameterValueMapping).GetMethod(nameof(ToImmutable), BindingFlags.Static | BindingFlags.NonPublic)!
                        .MakeGenericMethod(child.Table.Type).Invoke(null, [array]);
            }
        }
        var constructor = table.Type.GetConstructors().OrderByDescending(x => x.GetParameters().Length)
            .First(x => x.GetParameters().All(p => fields.ContainsKey(p.Name!)));
        var instance = constructor.Invoke(constructor.GetParameters().Select(x => fields[x.Name!]).ToArray());
        foreach (var property in table.Type.GetProperties().Where(x => x.SetMethod is not null && fields.ContainsKey(x.Name)))
            property.SetValue(instance, fields[property.Name]);
        return instance;
    }
    static ImmutableArray<T> ToImmutable<T>(T[] values) => ImmutableArray.CreateRange(values);
    static object? ConvertValue(object? value, Type type)
    {
        if (value is null or DBNull) return null;
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsInstanceOfType(value)) return value;
        if (type.IsEnum) return Enum.ToObject(type, value);
        if (type == typeof(DateTime) && value is long ticks) return DateTime.FromBinary(ticks);
        if (type == typeof(DateTimeOffset) && value is string text) return DateTimeOffset.ParseExact(text,"O",System.Globalization.CultureInfo.InvariantCulture);
        return Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
    }
}
