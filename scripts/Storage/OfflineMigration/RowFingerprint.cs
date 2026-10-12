using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Storage.Tools;

/// <summary>Order-independent row count and SHA-256 sum, including every selected key/value.</summary>
internal sealed class RowFingerprint
{
    static readonly BigInteger Modulus = BigInteger.One << 256;
    BigInteger sum;
    public long Count { get; private set; }
    public string Digest => sum.ToString("x64", CultureInfo.InvariantCulture);

    public void Add(IEnumerable<object?> values)
    {
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, leaveOpen: true);
        foreach (var value in values) Write(writer, value);
        writer.Flush();
        sum = (sum + new BigInteger(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length))), isUnsigned: true, isBigEndian: true)) % Modulus;
        Count++;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "MA0051", Justification = "The explicit tagged primitive codec preserves migration values without serializing credentials or runtime type names.")]
    static void Write(BinaryWriter writer, object? value)
    {
        if (value is null or DBNull) { writer.Write((byte)0); return; }
        if (value is TimeOnly timeOfDay) { Write(writer, timeOfDay.ToTimeSpan()); return; }
        switch (value)
        {
            case string text: writer.Write((byte)1); writer.Write(text); break;
            case byte[] blob: writer.Write((byte)2); writer.Write(blob.Length); writer.Write(blob); break;
            case Guid id: writer.Write((byte)3); writer.Write(id.ToByteArray()); break;
            case TimeSpan time: writer.Write((byte)12); writer.Write(time.Ticks); break;
            case DateOnly date: writer.Write((byte)4); writer.Write(date.DayNumber); break;
            case DateTime time: writer.Write((byte)5); writer.Write(time.ToUniversalTime().Ticks); break;
            case bool flag: writer.Write((byte)6); writer.Write(flag); break;
            case decimal number: writer.Write((byte)7); writer.Write(number.ToString("G29", CultureInfo.InvariantCulture)); break;
            case double number: writer.Write((byte)8); writer.Write(number); break;
            case float number: writer.Write((byte)9); writer.Write(number); break;
            case short or int or long: writer.Write((byte)10); writer.Write(Convert.ToInt64(value, CultureInfo.InvariantCulture)); break;
            case Array array:
                writer.Write((byte)11); writer.Write(array.Length);
                foreach (var element in array) Write(writer, element);
                break;
            default: throw new InvalidDataException("Unsupported migration fingerprint type: " + value.GetType().FullName);
        }
    }
}
