using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;

public static class StrategyCatalogValidation
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
    public const int MaximumDefinitionBytes = 262144;
    public const int MaximumChildren = 128;

    public static void ValidateKey(CatalogKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Require(Enum.IsDefined(key.Kind) && key.Id != Guid.Empty && key.Version > 0, "Invalid catalog identity/version.");
    }

    /// <summary>Defensively freezes and canonicalizes authoring input before asynchronous work starts.</summary>
    public static StrategyCatalogDefinition Freeze(StrategyCatalogDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var json = JsonSerializer.Serialize(source, JsonOptions);
        Require(Encoding.UTF8.GetByteCount(json) <= MaximumDefinitionBytes, "Catalog definition exceeds byte limit.");
        var d = JsonSerializer.Deserialize<StrategyCatalogDefinition>(json, JsonOptions)!;
        ValidateKey(d.Key);
        Token(d.Code); Text(d.Name, 200); Text(d.Description, 4096, true);
        Require(d.SchemaVersion == 1, "Unsupported catalog schema version.");
        Require(d.Settings.ValueKind == JsonValueKind.Object, "Settings must be a JSON object.");
        _ = CanonicalJson(d.Settings); // Reject duplicate properties and unsupported numbers.
        var kind = d.Key.Kind;
        var expectedParent = kind switch
        {
            StrategyCatalogKind.Variant => StrategyCatalogKind.Structure,
            StrategyCatalogKind.ParameterSet => StrategyCatalogKind.ParameterSchema,
            StrategyCatalogKind.Deployment => StrategyCatalogKind.Strategy,
            _ => (StrategyCatalogKind?)null
        };
        Require((d.Parent is null) == (expectedParent is null), "Invalid parent reference for catalog kind.");
        if (d.Parent is not null) { ValidateKey(d.Parent); Require(d.Parent.Kind == expectedParent, "Incorrect parent kind."); }
        Require(kind == StrategyCatalogKind.Deployment
            ? d.Horizon is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly
            : d.Horizon == TimeFrameType.None, "Only deployments carry a Daily, Weekly or Monthly horizon.");
        if (kind == StrategyCatalogKind.Variant)
        {
            Token(d.Side); Token(d.Bias); Token(d.PremiumMode);
        }
        else Require(d.Side == "" && d.Bias == "" && d.PremiumMode == "", "Variant traits require a variant.");

        ValidateKeys(d.Families, kind == StrategyCatalogKind.Strategy, StrategyCatalogKind.Family);
        ValidateKeys(d.Structures, kind == StrategyCatalogKind.Strategy, StrategyCatalogKind.Structure);
        ValidateKeys(d.Variants, kind == StrategyCatalogKind.Deployment, StrategyCatalogKind.Variant);
        Children(d.Capabilities, c => c.Role + ":" + c.Code + ":" + c.Version);
        foreach (var c in d.Capabilities)
        {
            Require(c.Role is "evaluator" or "builder" or "validator" or "risk" or "data", "Unknown capability role.");
            Token(c.Code); Require(c.Version > 0, "Capability version must be positive.");
        }
        Require(kind != StrategyCatalogKind.Family || d.Capabilities.Length == 0, "Family grouping is not executable.");
        Children(d.ExpiryGroups, x => x.Key); Children(d.Legs, x => x.Key);
        Require(kind == StrategyCatalogKind.Structure || (d.ExpiryGroups.Length == 0 && d.Legs.Length == 0), "Leg topology belongs to structures.");
        foreach (var group in d.ExpiryGroups)
        {
            Token(group.Key);
            var seen = new HashSet<string>(StringComparer.Ordinal) { group.Key };
            var next = group.AfterGroup;
            while (next is not null)
            {
                Require(seen.Add(next), "Cyclic expiry groups.");
                var parent = d.ExpiryGroups.SingleOrDefault(x => x.Key == next);
                Require(parent is not null, "Missing expiry group reference.");
                next = parent!.AfterGroup;
            }
        }
        foreach (var leg in d.Legs)
        {
            Token(leg.Key); Require(leg.InstrumentClass is "Futures" or "FuturesOption", "Unsupported instrument class.");
            SideAndRatio(leg.Side, leg.Ratio);
            Require(leg.InstrumentClass == "Futures" ? leg.OptionRight == "None" : leg.OptionRight is "Call" or "Put", "Invalid option right.");
            Require(d.ExpiryGroups.Any(g => g.Key == leg.ExpiryGroup), "Leg references a missing expiry group.");
        }
        Children(d.VariantLegs, x => x.LegKey);
        Require(kind == StrategyCatalogKind.Variant || d.VariantLegs.Length == 0, "Variant leg rules require a variant.");
        foreach (var leg in d.VariantLegs) { Token(leg.LegKey); SideAndRatio(leg.Side, leg.Ratio); }
        Children(d.Products, x => x.ProductId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var p in d.Products)
        {
            Require(p.ProductId > 0, "Product ID must be positive.");
            Text(p.Symbol, 100); Text(p.Exchange, 100); Text(p.Currency, 12);
        }
        Children(d.PipelineParameters, x => x.Role); Children(d.Parameters, x => x.Role);
        Require(!d.Parameters.Select(x => x.Role).Intersect(d.PipelineParameters.Select(x => x.Role), StringComparer.Ordinal).Any(), "Parameter roles must be unique across sources.");
        foreach (var p in d.PipelineParameters)
        {
            Token(p.Role); Require(Enum.IsDefined(p.Kind) && p.Id != Guid.Empty && p.Version > 0, "Invalid pipeline parameter reference.");
            Hash(p.Hash);
        }
        foreach (var p in d.Parameters) { Token(p.Role); ValidateKey(p.ParameterSet); Require(p.ParameterSet.Kind == StrategyCatalogKind.ParameterSet, "Expected catalog parameter set."); }
        Children(d.LegacyFamilies, x => $"{x.Id}:{x.Version}");
        foreach (var f in d.LegacyFamilies) Require(f.Id > 0 && f.Version > 0, "Invalid legacy family reference.");
        Require(kind == StrategyCatalogKind.Deployment || (d.Products.Length == 0 && d.PipelineParameters.Length == 0 && d.Parameters.Length == 0 && d.LegacyFamilies.Length == 0), "Deployment relationships require a deployment.");
        if (kind == StrategyCatalogKind.ParameterSchema) ReadShape(d.Settings);
        if (kind == StrategyCatalogKind.Family) Require(!d.Settings.EnumerateObject().Any(), "Families contain grouping metadata only.");
        return d with
        {
            Families = Sort(d.Families), Structures = Sort(d.Structures), Variants = Sort(d.Variants),
            Capabilities = d.Capabilities.OrderBy(c => c.Role, StringComparer.Ordinal).ThenBy(c => c.Code, StringComparer.Ordinal).ThenBy(c => c.Version).ToArray(),
            ExpiryGroups = d.ExpiryGroups.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(),
            Legs = d.Legs.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(),
            VariantLegs = d.VariantLegs.OrderBy(x => x.LegKey, StringComparer.Ordinal).ToArray(),
            Products = d.Products.OrderBy(x => x.ProductId).ToArray(),
            PipelineParameters = d.PipelineParameters.OrderBy(x => x.Role, StringComparer.Ordinal).ToArray(),
            Parameters = d.Parameters.OrderBy(x => x.Role, StringComparer.Ordinal).ToArray(),
            LegacyFamilies = d.LegacyFamilies.OrderBy(x => x.Id).ThenBy(x => x.Version).ToArray()
        };
    }

    public static string ContentHash(StrategyCatalogDefinition definition) => Sha(CanonicalJson(JsonSerializer.SerializeToElement(Freeze(definition), JsonOptions)));
    internal static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static CatalogKey[] Dependencies(StrategyCatalogDefinition d) =>
        (d.Parent is null ? Enumerable.Empty<CatalogKey>() : [d.Parent]).Concat(d.Families).Concat(d.Structures)
        .Concat(d.Variants).Concat(d.Parameters.Select(p => p.ParameterSet)).Distinct().ToArray();

    internal static void ValidateForPublication(StrategyCatalogDefinition d, IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> graph)
    {
        void Needs(string role) => Require(d.Capabilities.Any(x => x.Role == role), $"A {d.Key.Kind} requires a {role} capability.");
        switch (d.Key.Kind)
        {
            case StrategyCatalogKind.Strategy:
                Require(d.Families.Length > 0 && d.Structures.Length > 0, "A strategy requires families and supported structures.");
                Needs("evaluator"); Needs("data"); break;
            case StrategyCatalogKind.Structure:
                Require(d.Legs.Length > 0, "A structure requires legs."); Needs("builder"); Needs("risk"); break;
            case StrategyCatalogKind.Variant:
                var structure = graph[d.Parent!].Definition;
                Require(d.VariantLegs.All(l => structure.Legs.Any(s => s.Key == l.LegKey)), "Variant leg does not belong to its exact structure.");
                Needs("validator"); break;
            case StrategyCatalogKind.ParameterSchema:
                Needs("validator"); break;
            case StrategyCatalogKind.ParameterSet:
                ValidateParameters(ReadShape(graph[d.Parent!].Definition.Settings), d.Settings); break;
            case StrategyCatalogKind.Deployment:
                Require(d.Products.Length > 0 && d.Variants.Length > 0, "A deployment requires products and variants.");
                var strategy = graph[d.Parent!].Definition;
                Require(d.Variants.All(v => strategy.Structures.Contains(graph[v].Definition.Parent!)), "Deployment variant structure is not assigned to the exact strategy version.");
                Needs("validator"); break;
        }
    }

    public static CatalogParameterShape ReadShape(JsonElement json)
    {
        var shape = json.Deserialize<CatalogParameterShape>(JsonOptions) ?? throw new ArgumentException("Missing parameter shape.");
        ValidateShape(shape, 0);
        Require(shape.Type == CatalogValueType.Object, "Parameter schema root must be Object.");
        return shape;
    }

    static void ValidateShape(CatalogParameterShape s, int depth)
    {
        Require(depth < 16 && Enum.IsDefined(s.Type), "Invalid or too deeply nested parameter shape.");
        Require(s.Properties is not null && s.Required is not null && s.Choices is not null, "Shape collections cannot be null.");
        Require(s.Properties!.Count <= MaximumChildren && s.Required!.Length <= MaximumChildren && s.Choices!.Length <= MaximumChildren, "Shape is too large.");
        Require(s.Minimum is null || s.Maximum is null || s.Minimum <= s.Maximum, "Invalid numeric bounds.");
        Require((s.MinLength is null || s.MinLength >= 0) && (s.MaxLength is null || s.MaxLength >= 0) &&
            (s.MinLength is null || s.MaxLength is null || s.MinLength <= s.MaxLength), "Invalid length bounds.");
        Require(s.Type is CatalogValueType.Decimal or CatalogValueType.Integer || (s.Minimum is null && s.Maximum is null), "Numeric bounds require a number.");
        Require(s.Type is CatalogValueType.String or CatalogValueType.Array || (s.MinLength is null && s.MaxLength is null), "Length bounds require String or Array.");
        Require(s.Type == CatalogValueType.String || s.Choices!.Length == 0, "Choices require String.");
        Require(s.Type == CatalogValueType.Object || (s.Properties.Count == 0 && s.Required!.Length == 0), "Properties require Object.");
        Require((s.Type == CatalogValueType.Array) == (s.Items is not null), "Array requires an item shape.");
        Require(s.Required!.Distinct(StringComparer.Ordinal).Count() == s.Required.Length && s.Required.All(s.Properties.ContainsKey), "Invalid required properties.");
        Require(s.Choices!.Distinct(StringComparer.Ordinal).Count() == s.Choices.Length, "Duplicate choices.");
        Text(s.Unit, 80, true);
        foreach (var (key, value) in s.Properties) { Token(key); Require(value is not null, "Missing property shape."); ValidateShape(value!, depth + 1); }
        if (s.Items is not null) ValidateShape(s.Items, depth + 1);
    }

    public static void ValidateParameters(CatalogParameterShape shape, JsonElement value)
    {
        ValidateShape(shape, 0);
        _ = CanonicalJson(value);
        ValidateValue(shape, value, "$", 0);
    }

    static void ValidateValue(CatalogParameterShape s, JsonElement v, string path, int depth)
    {
        Require(depth < 16, "Parameter nesting exceeds limit.");
        switch (s.Type)
        {
            case CatalogValueType.Object:
                Require(v.ValueKind == JsonValueKind.Object, $"{path} must be Object.");
                Require(s.Required.All(x => v.TryGetProperty(x, out _)), $"{path} has missing required fields.");
                foreach (var p in v.EnumerateObject())
                {
                    Require(s.Properties.TryGetValue(p.Name, out var child), $"{path}.{p.Name} is unknown.");
                    ValidateValue(child!, p.Value, path + "." + p.Name, depth + 1);
                }
                break;
            case CatalogValueType.Array:
                Require(v.ValueKind == JsonValueKind.Array, $"{path} must be Array."); Length(v.GetArrayLength());
                foreach (var item in v.EnumerateArray()) ValidateValue(s.Items!, item, path + "[]", depth + 1);
                break;
            case CatalogValueType.String:
                Require(v.ValueKind == JsonValueKind.String, $"{path} must be String.");
                Length(v.GetString()!.Length); Require(s.Choices.Length == 0 || s.Choices.Contains(v.GetString(), StringComparer.Ordinal), $"{path} has an invalid choice."); break;
            case CatalogValueType.Decimal:
            case CatalogValueType.Integer:
                Require(v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out _), $"{path} must be a decimal number.");
                var n = v.GetDecimal();
                Require(s.Type != CatalogValueType.Integer || n == decimal.Truncate(n), $"{path} must be Integer.");
                Require((s.Minimum is null || n >= s.Minimum) && (s.Maximum is null || n <= s.Maximum), $"{path} is outside numeric bounds."); break;
            case CatalogValueType.Boolean: Require(v.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{path} must be Boolean."); break;
        }
        void Length(int length) => Require((s.MinLength is null || length >= s.MinLength) && (s.MaxLength is null || length <= s.MaxLength), $"{path} is outside length bounds.");
    }

    internal static string CanonicalJson(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value);
        return Encoding.UTF8.GetString(stream.ToArray());
        static void Write(Utf8JsonWriter w, JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    w.WriteStartObject();
                    var properties = e.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
                    Require(properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == properties.Length, "Duplicate JSON property.");
                    foreach (var p in properties) { w.WritePropertyName(p.Name); Write(w, p.Value); }
                    w.WriteEndObject(); break;
                case JsonValueKind.Array:
                    w.WriteStartArray(); foreach (var item in e.EnumerateArray()) Write(w, item); w.WriteEndArray(); break;
                case JsonValueKind.Number:
                    Require(e.TryGetDecimal(out _), "JSON number exceeds decimal precision/range.");
                    var number = e.GetDecimal().ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
                    Require(NumericIdentity(e.GetRawText()) == NumericIdentity(number), "JSON number loses decimal precision.");
                    w.WriteRawValue(number); break;
                default: e.WriteTo(w); break;
            }
        }
    }

    static string NumericIdentity(string number)
    {
        var parts = number.ToLowerInvariant().Split('e');
        var exponent = parts.Length == 2 ? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
        var mantissa = parts[0];
        var negative = mantissa.StartsWith('-');
        if (negative) mantissa = mantissa[1..];
        var dot = mantissa.IndexOf('.');
        var scale = checked((dot < 0 ? 0 : mantissa.Length - dot - 1) - exponent);
        var digits = mantissa.Replace(".", "").TrimStart('0');
        if (digits.Length == 0) return "0";
        while (digits.EndsWith('0')) { digits = digits[..^1]; scale--; }
        return (negative ? "-" : "") + digits + "e" + (-scale).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static void Utc(DateTime value) => Require(value.Kind == DateTimeKind.Utc && value != default && value.Ticks % 10 == 0, "Use non-default UTC timestamps with PostgreSQL microsecond precision.");
    internal static void Hash(string value) => Require(value is not null && Regex.IsMatch(value, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant), "Invalid lowercase SHA-256 hash.");
    internal static void Token(string value) => Require(value is not null && Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$", RegexOptions.CultureInvariant), "Invalid catalog code/key.");
    internal static void Text(string value, int limit, bool empty = false) => Require(value is not null && value.Length <= limit && (empty || !string.IsNullOrWhiteSpace(value)), "Invalid catalog text.");
    internal static void Require(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
    static void SideAndRatio(string side, decimal ratio) => Require(side is "Buy" or "Sell" && ratio > 0 && ratio <= 1000000, "Invalid leg side/ratio.");
    static void Children<T>(T[] values, Func<T, string> key)
    {
        Require(values is not null && values.Length <= MaximumChildren, "Missing or oversized child collection.");
        Require(values!.All(x => x is not null), "Null child entry.");
        Require(values.Select(key).Distinct(StringComparer.Ordinal).Count() == values.Length, "Duplicate child key.");
    }
    static void ValidateKeys(CatalogKey[] keys, bool permitted, StrategyCatalogKind kind)
    {
        Children(keys, x => $"{x.Kind}:{x.Id}:{x.Version}"); Require(permitted || keys.Length == 0, "Relationship is invalid for this catalog kind.");
        foreach (var key in keys) { ValidateKey(key); Require(key.Kind == kind, "Incorrect relationship kind."); }
    }
    static CatalogKey[] Sort(CatalogKey[] keys) => keys.OrderBy(x => x.Kind).ThenBy(x => x.Id).ThenBy(x => x.Version).ToArray();
}
