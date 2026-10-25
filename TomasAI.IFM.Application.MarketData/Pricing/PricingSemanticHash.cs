using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Hashes typed values directly, preserving the established canonical encoding and digest identities.</summary>
public static class PricingSemanticHash
{
    static readonly JsonSerializerOptions Options = CreateOptions();
    static readonly ConditionalWeakTable<OptionPricingContext, string> ContextDigests = new();

    /// <summary>Streams canonical typed properties into SHA-256 without a JSON document or complete output buffer.</summary>
    public static string Compute<T>(T value) => value is OptionPricingContext context
        ? ContextDigests.GetValue(context, static item => ComputeCore(item)) : ComputeCore(value);

    static string ComputeCore<T>(T value)
    {
        using var output = new HashBufferWriter();
        using (var writer = new Utf8JsonWriter(output))
        {
            JsonSerializer.Serialize(writer, value, Options);
            writer.Flush();
        }
        return output.Digest();
    }

    static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(type =>
        {
            if (type.Kind != JsonTypeInfoKind.Object) return;
            var properties = type.Properties.OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
            type.Properties.Clear();
            foreach (var property in properties)
            { property.Order = 0; type.Properties.Add(property); }
        });
        var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
        options.Converters.Add(new CanonicalDecimalConverter());
        options.Converters.Add(new CanonicalDoubleConverter());
        options.Converters.Add(new CanonicalSingleConverter());
        options.Converters.Add(new CanonicalByteArrayConverter());
        options.Converters.Add(new CanonicalElementConverter());
        options.Converters.Add(new CanonicalDateTimeOffsetConverter());
        options.Converters.Add(new CanonicalDateTimeConverter());
        options.Converters.Add(new CanonicalDictionaryConverterFactory());
        options.MakeReadOnly();
        return options;
    }

    // Preserve the old numeric normalization, including float-to-double exponent formatting.
    static void WriteNumber(Utf8JsonWriter writer, ReadOnlySpan<char> text)
    {
        Span<char> normalized = stackalloc char[64];
        if (text.IndexOfAny('e', 'E') < 0 && decimal.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number))
        {
            number.TryFormat(normalized, out var length, "G29", CultureInfo.InvariantCulture);
            writer.WriteRawValue(normalized[..length]);
        }
        else
        {
            var value = double.Parse(text, CultureInfo.InvariantCulture);
            value.TryFormat(normalized, out var length, "R", CultureInfo.InvariantCulture);
            writer.WriteRawValue(normalized[..length]);
        }
    }

    sealed class CanonicalDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            Span<char> text = stackalloc char[64];
            value.TryFormat(text, out var length, "G29", CultureInfo.InvariantCulture);
            writer.WriteRawValue(text[..length]);
        }
    }

    sealed class CanonicalDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDouble();
        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            Span<char> text = stackalloc char[64];
            value.TryFormat(text, out var length, "R", CultureInfo.InvariantCulture);
            WriteNumber(writer, text[..length]);
        }
    }

    sealed class CanonicalSingleConverter : JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetSingle();
        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        {
            Span<char> text = stackalloc char[64];
            value.TryFormat(text, out var length, "R", CultureInfo.InvariantCulture);
            WriteNumber(writer, text[..length]);
        }
    }

    sealed class CanonicalByteArrayConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetBytesFromBase64();
        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
            => writer.WriteStringValue(Convert.ToBase64String(value));
    }

    sealed class CanonicalDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDateTimeOffset();
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture));
    }

    sealed class CanonicalDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDateTime();
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture));
    }

    // Explicit JsonElement callers retain canonical behavior; typed pricing models never take this path.
    sealed class CanonicalElementConverter : JsonConverter<JsonElement>
    {
        public override JsonElement Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }
        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                    { writer.WritePropertyName(property.Name); Write(writer, property.Value, options); }
                    writer.WriteEndObject(); break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in value.EnumerateArray()) Write(writer, item, options);
                    writer.WriteEndArray(); break;
                case JsonValueKind.Number: WriteNumber(writer, value.GetRawText()); break;
                default: value.WriteTo(writer); break;
            }
        }
    }

    sealed class CanonicalDictionaryConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type type) => DictionaryInterface(type) is not null;
        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        {
            var arguments = DictionaryInterface(type)!.GetGenericArguments();
            return (JsonConverter)Activator.CreateInstance(typeof(CanonicalDictionaryConverter<,,>)
                .MakeGenericType(type, arguments[0], arguments[1]))!;
        }
        static Type? DictionaryInterface(Type type) => type.GetInterfaces().Prepend(type).FirstOrDefault(
            candidate => candidate.IsGenericType && (candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                || candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
    }

    sealed class CanonicalDictionaryConverter<TDictionary, TKey, TValue> : JsonConverter<TDictionary>
    {
        public override TDictionary? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            => throw new NotSupportedException("Canonical hashing is write-only.");
        public override void Write(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
        {
            // Dictionary keys are uncommon in pricing models. Use the standard key converter so their
            // canonical names remain compatible for string, numeric, enum and date key types.
            var keyConverter = (JsonConverter<TKey>)JsonSerializerOptions.Default.GetConverter(typeof(TKey));
            var entries = ((IEnumerable<KeyValuePair<TKey, TValue>>)(object)value!).Select(pair =>
            {
                if (pair.Key is string name) return (Name: name, pair.Value);
                using var buffer = new MemoryStream();
                using (var keyWriter = new Utf8JsonWriter(buffer))
                {
                    keyWriter.WriteStartObject(); keyConverter.WriteAsPropertyName(keyWriter, pair.Key, options);
                    keyWriter.WriteNullValue(); keyWriter.WriteEndObject(); keyWriter.Flush();
                }
                using var document = JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length)));
                return (Name: document.RootElement.EnumerateObject().First().Name, pair.Value);
            }).OrderBy(pair => pair.Name, StringComparer.Ordinal);
            writer.WriteStartObject();
            foreach (var pair in entries)
            { writer.WritePropertyName(pair.Name); JsonSerializer.Serialize(writer, pair.Value, options); }
            writer.WriteEndObject();
        }
    }

    sealed class HashBufferWriter : IBufferWriter<byte>, IDisposable
    {
        readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            hash.AppendData(buffer.AsSpan(0, count));
        }
        public Memory<byte> GetMemory(int sizeHint = 0) { Ensure(sizeHint); return buffer; }
        public Span<byte> GetSpan(int sizeHint = 0) { Ensure(sizeHint); return buffer; }
        void Ensure(int sizeHint)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            if (sizeHint <= buffer.Length) return;
            var replacement = ArrayPool<byte>.Shared.Rent(sizeHint);
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            buffer = replacement;
        }
        public string Digest()
        {
            Span<byte> digest = stackalloc byte[32];
            hash.GetHashAndReset(digest);
            return Convert.ToHexStringLower(digest);
        }
        public void Dispose()
        { hash.Dispose(); ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
}
