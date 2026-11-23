using System.Security.Cryptography;
using System.Text.Json;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

public static class MarketConditionAssessmentHash
{
    static readonly JsonSerializerOptions Options = CreateOptions();
    static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new CanonicalDecimalConverter());
        return options;
    }
    sealed class CanonicalDecimalConverter : System.Text.Json.Serialization.JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
            => writer.WriteRawValue(value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));
    }
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static string Compute<T>(T value) => ComputeWithSize(value).Hash;

    // The existing serializer options define the bytes and digest for persisted v1 content.
    internal static (string Hash, int Size) ComputeWithSize<T>(T value)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stream = new DigestStream(hash);
        JsonSerializer.Serialize(stream, value, Options);
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        hash.GetHashAndReset(digest);
        return (Convert.ToHexString(digest), checked((int)stream.Length));
    }

    sealed class DigestStream(IncrementalHash hash) : Stream
    {
        long _length;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position { get => _length; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> bytes)
        {
            hash.AppendData(bytes);
            _length += bytes.Length;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    public static string Parameters(MarketConditionAssessmentParameterSet p)
    {
        ArgumentNullException.ThrowIfNull(p);
        p.Validate();
        return Compute(p with { Sources = p.Sources });
    }
}
