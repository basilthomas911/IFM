using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Compiled schema authority shared by authoring, queries and database registration.</summary>
public sealed class ParameterSchemaRegistry
{
    public const string StrategyOptionChainCacheComponent = "market-data.strategy-option-chain-cache";
    public const string RegimeComponent = "strategy-workflow.regime-discovery";
    public const string FuturesItiSignalComponent = "market-data-analytics.futures-iti-signal";
    public const string OptionVolatilitySeriesComponent = "option-volatility.series";
    public const string OptionVolatilityConsumerRulesComponent = "option-volatility.consumer-rules";
    public const string OptionVolatilityRetentionComponent = "option-volatility.retention";
    public const string IronCondorMarketSelectionComponent = "option-spread-strategy.iron-condor-defaults";
    public const string VerticalSpreadMarketSelectionComponent = "option-spread-strategy.vertical-spread-defaults";
    public const int CurrentRegimeSchemaVersion = 5;
    public const int CurrentFuturesItiSignalSchemaVersion = 1;
    public const int CurrentOptionVolatilitySeriesSchemaVersion = 1;
    public const int CurrentOptionVolatilityConsumerRulesSchemaVersion = 1;
    public const int CurrentOptionVolatilityRetentionSchemaVersion = 1;
    public const int CurrentIronCondorMarketSelectionSchemaVersion = 1;
    public const int CurrentVerticalSpreadMarketSelectionSchemaVersion = 1;
    static readonly System.Collections.Concurrent.ConcurrentDictionary<PropertyInfo, NullabilityInfo> Nullability = new();
    public static ParameterSchemaRegistry Default { get; } = CreateDefault();
    readonly Dictionary<(string, int), (Type Type, ParameterSchemaDefinition Definition)> schemas = new();
    readonly HashSet<(string Component, int Version)> strictNullabilitySchemas = [];

    static ParameterSchemaRegistry CreateDefault()
    {
        var registry = new ParameterSchemaRegistry(
            Enumerable.Range(1, 3).Select(version => (RegimeComponent, version, typeof(RegimeDiscoveryParameterSet))));
        registry.RegisterStrict(RegimeComponent, 4, typeof(RegimeDiscoveryParameterSet));
        registry.RegisterStrict(RegimeComponent, CurrentRegimeSchemaVersion, typeof(RegimeDiscoveryParameterSet));
        registry.RegisterStrict(
            FuturesItiSignalComponent,
            CurrentFuturesItiSignalSchemaVersion,
            typeof(FuturesItiSignalParameterSet));
        registry.RegisterStrict(
            OptionVolatilitySeriesComponent,
            CurrentOptionVolatilitySeriesSchemaVersion,
            typeof(OptionVolatilitySeriesParameterSet));
        registry.RegisterStrict(
            OptionVolatilityConsumerRulesComponent,
            CurrentOptionVolatilityConsumerRulesSchemaVersion,
            typeof(OptionVolatilityConsumerRulesParameterSet));
        registry.RegisterStrict(
            OptionVolatilityRetentionComponent,
            CurrentOptionVolatilityRetentionSchemaVersion,
            typeof(OptionVolatilityRetentionParameterSet));
        registry.RegisterStrict(
            IronCondorMarketSelectionComponent,
            CurrentIronCondorMarketSelectionSchemaVersion,
            typeof(IronCondorMarketSelectionParameterSet));
        registry.RegisterStrict(
            VerticalSpreadMarketSelectionComponent,
            CurrentVerticalSpreadMarketSelectionSchemaVersion,
            typeof(VerticalSpreadMarketSelectionParameterSet));
        registry.RegisterStrict(StrategyOptionChainCacheComponent, 1, typeof(TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet));
        return registry;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1308", Justification = "Lowercase hexadecimal digests and catalog keys are persisted contract values.")]
    void RegisterStrict(string component, int version, Type payloadType)
    {
        var shape = ShapeWithNullability(payloadType, version);
        shape["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        shape["required"] = new[] { "SchemaVersion" };
        var properties = (Dictionary<string, object?>)shape["properties"]!;
        properties["SchemaVersion"] = new Dictionary<string, object?>(StringComparer.Ordinal) { { "type", "integer" }, { "const", version } };
        var json = JsonSerializer.Serialize(shape);
        var definition = new ParameterSchemaDefinition(component, version, "sorted-json-v1", json,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant());
        if (!schemas.TryAdd((component, version), (payloadType, definition))) throw new ArgumentException("Duplicate schema registration.",nameof(component));
        strictNullabilitySchemas.Add((component, version));
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1308", Justification = "Lowercase hexadecimal digests and catalog keys are persisted contract values.")]
    public ParameterSchemaRegistry(IEnumerable<(string Component, int Version, Type PayloadType)> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        foreach (var item in registrations)
        {
            if (string.IsNullOrWhiteSpace(item.Component) || item.Version <= 0) throw new ArgumentException("Invalid schema registration.",nameof(registrations));
            var shape = Shape(item.PayloadType, item.Version);
            shape["$schema"] = "https://json-schema.org/draft/2020-12/schema";
            shape["required"] = new[] { "SchemaVersion" };
            var properties = (Dictionary<string, object?>)shape["properties"]!;
            properties["SchemaVersion"] = new Dictionary<string, object?>(StringComparer.Ordinal) { { "type", "integer" }, { "const", item.Version } };
            var json = JsonSerializer.Serialize(shape);
            var definition = new ParameterSchemaDefinition(item.Component, item.Version, "sorted-json-v1", json,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant());
            if (!schemas.TryAdd((item.Component, item.Version), (item.PayloadType, definition))) throw new ArgumentException("Duplicate schema registration.",nameof(registrations));
        }
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2365", Justification = "The published registry property exposes a detached immutable-by-convention snapshot.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serialization contract uses arrays; preserve its wire shape.")]
    public ParameterSchemaDefinition[] Definitions => schemas.Values.Select(x => x.Definition).OrderBy(x => x.ComponentCode, StringComparer.Ordinal).ThenBy(x => x.Version).ToArray();
    public ParameterSchemaDefinition Get(string component, int version) => schemas.TryGetValue((component, version), out var schema)
        ? schema.Definition : throw new ArgumentException("PARAM.SCHEMA_UNSUPPORTED",nameof(component));
    public ParameterValidationIssue[] ValidateStructure(string component, int version, string json)
    {
        if (!schemas.TryGetValue((component, version), out var schema)) return [new("PARAM.SCHEMA_UNSUPPORTED", "SchemaVersion", "The schema is not registered.")];
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            var issues = new List<ParameterValidationIssue>();
            Check(document.RootElement, schema.Type, "Payload", issues, version, strictNullabilitySchemas.Contains((component, version)));
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("SchemaVersion", out var declared) || declared.ValueKind != JsonValueKind.Number || !declared.TryGetInt32(out var number) || number != version)
                issues.Add(new("PARAM.SCHEMA_MISMATCH", "SchemaVersion", "The payload must declare the selected schema version."));
            return issues.ToArray();
        }
        catch (JsonException error) { return [new("PARAM.STRUCTURE_INVALID", "Payload", error.Message)]; }
    }
    /// <summary>Typed editors must not silently discard unrecognized fields.</summary>
    public bool CanEditLosslessly(string component, int version, string json)
    {
        if (!schemas.TryGetValue((component, version), out var schema)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return ValidateStructure(component, version, json).Length == 0 && Known(document.RootElement, schema.Type, version);
        }
        catch (JsonException) { return false; }
    }
    static Type? CollectionElement(Type type) => (type.IsArray) switch { true => type.GetElementType(), false => (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Immutable.ImmutableArray<>)) switch { true => type.GetGenericArguments()[0], false => null } };
    static bool Known(JsonElement value, Type type, int schemaVersion, NullabilityInfo? nullability = null)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (value.ValueKind == JsonValueKind.Null) return nullable is not null || nullability?.ReadState == NullabilityState.Nullable;
        type = nullable ?? type;
        if (CollectionElement(type) is { } element) return value.EnumerateArray().All(item => Known(item, element, schemaVersion, nullability?.ElementType));
        if (value.ValueKind != JsonValueKind.Object) return true;
        var properties = Properties(type, schemaVersion).ToDictionary(x => x.Name, StringComparer.Ordinal);
        return value.EnumerateObject().All(property => properties.TryGetValue(property.Name, out var known) && Known(property.Value, known.PropertyType, schemaVersion, Nullability.GetOrAdd(known, property => new NullabilityInfoContext().Create(property))));
    }
    static PropertyInfo[] Properties(Type type, int schemaVersion) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetIndexParameters().Length == 0 && (x.GetCustomAttribute<ParameterSchemaSinceAttribute>() is not { } since || since.Version <= schemaVersion)).OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
    static Dictionary<string, object?> Shape(Type type, int schemaVersion)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying) return new(StringComparer.Ordinal) { { "anyOf", new object[] { Shape(underlying, schemaVersion), new { type = "null" } } } };
        if (type.IsEnum) return new(StringComparer.Ordinal) { { "type", "integer" }, { "enum", Enum.GetValues(type).Cast<object>().Select(Convert.ToInt64).ToArray() } };
        if (type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime)) return new(StringComparer.Ordinal) { { "type", "string" } };
        if (type == typeof(bool)) return new(StringComparer.Ordinal) { { "type", "boolean" } };
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return new(StringComparer.Ordinal) { { "type", "number" } };
        if (type.IsPrimitive) return new(StringComparer.Ordinal) { { "type", "integer" } };
        if (CollectionElement(type) is { } element) return new(StringComparer.Ordinal) { { "type", "array" }, { "items", Shape(element, schemaVersion) } };
        return new(StringComparer.Ordinal) { { "type", "object" }, { "additionalProperties", true }, { "properties", Properties(type, schemaVersion).ToDictionary(x => x.Name, x => (object?)Shape(x.PropertyType, schemaVersion), StringComparer.Ordinal) } };
    }
    static Dictionary<string, object?> ShapeWithNullability(Type type, int schemaVersion, NullabilityInfo? nullability = null)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null) return NullableShape(ShapeWithNullability(nullable, schemaVersion));
        Dictionary<string, object?> shape;
        if (type.IsEnum) shape = new(StringComparer.Ordinal) { { "type", "integer" }, { "enum", Enum.GetValues(type).Cast<object>().Select(Convert.ToInt64).ToArray() } };
        else if (type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime)) shape = new(StringComparer.Ordinal) { { "type", "string" } };
        else if (type == typeof(bool)) shape = new(StringComparer.Ordinal) { { "type", "boolean" } };
        else if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) shape = new(StringComparer.Ordinal) { { "type", "number" } };
        else if (type.IsPrimitive) shape = new(StringComparer.Ordinal) { { "type", "integer" } };
        else if (CollectionElement(type) is { } element) shape = new(StringComparer.Ordinal) { { "type", "array" }, { "items", ShapeWithNullability(element, schemaVersion, nullability?.ElementType) } };
        else shape = new(StringComparer.Ordinal) {{"type","object"},{"additionalProperties",true},{"properties",Properties(type,schemaVersion).ToDictionary(property=>property.Name,
            property=>(object?)ShapeWithNullability(property.PropertyType,schemaVersion,Nullability.GetOrAdd(property,p=>new NullabilityInfoContext().Create(p))),StringComparer.Ordinal)}};
        return nullability?.ReadState == NullabilityState.Nullable ? NullableShape(shape) : shape;
    }
    static Dictionary<string, object?> NullableShape(Dictionary<string, object?> shape) => new(StringComparer.Ordinal) {
        {"anyOf",new object[] {shape,new Dictionary<string, object?>(StringComparer.Ordinal) {{"type","null"}}}}
    };
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1075", Justification = "This is a JSON pointer separator, not an operating system file path.")]
    static void Check(JsonElement value, Type type, string path, List<ParameterValidationIssue> issues, int schemaVersion, bool strictNullability = false, NullabilityInfo? nullability = null)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (value.ValueKind == JsonValueKind.Null && (nullable is not null || (!strictNullability && !type.IsValueType) || nullability?.ReadState == NullabilityState.Nullable)) return;
        type = nullable ?? type;
        bool valid;
        if (type == typeof(string)) valid = value.ValueKind == JsonValueKind.String;
        else if (type == typeof(Guid)) valid = value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out _);
        else if (type == typeof(bool)) valid = value.ValueKind is JsonValueKind.True or JsonValueKind.False;
        else if (type.IsEnum) valid = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && Enum.IsDefined(type, Enum.ToObject(type, number));
        else if (type.IsPrimitive || type == typeof(decimal))
        {
            try { JsonSerializer.Deserialize(value.GetRawText(), type); valid = value.ValueKind == JsonValueKind.Number; } catch (JsonException) { valid = false; }
        }
        else if (CollectionElement(type) is { } element)
        {
            valid = value.ValueKind == JsonValueKind.Array;
            if (valid) { var index = 0; foreach (var item in value.EnumerateArray()) Check(item, element, path + "/" + index++, issues, schemaVersion, strictNullability, nullability?.ElementType); }
        }
        else
        {
            valid = value.ValueKind == JsonValueKind.Object;
            if (valid) foreach (var property in Properties(type, schemaVersion)) if (value.TryGetProperty(property.Name, out var child)) Check(child, property.PropertyType, path + "/" + property.Name, issues, schemaVersion, strictNullability, Nullability.GetOrAdd(property, p => new NullabilityInfoContext().Create(p)));
        }
        if (!valid) issues.Add(new("PARAM.STRUCTURE_INVALID", path, $"Expected {type.Name}."));
    }
}





