using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Bounded V2 transport metadata. Durable V1 JSON and absent legacy origins remain readable.</summary>
public static class MessageOriginBinary
{
    static readonly UTF8Encoding Utf8 = new(false, true);
    public static string Serialize(MessageOrigin origin) => origin.GetBinary();
    internal static string EncodeCore(MessageOrigin origin)
    {
        Span<byte> bytes = stackalloc byte[576];
        "IMO\x02"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[4..], origin.OriginatedAtUtc.UtcTicks);
        var length = Utf8.GetBytes(origin.ClockDomainId, bytes[15..]);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[12..], checked((ushort)length));
        bytes[14] = (byte)((origin.RunId.HasValue ? 1 : 0) | (origin.Generation.HasValue ? 2 : 0) | (origin.CausationId.HasValue ? 4 : 0));
        var offset = 15 + length;
        if (origin.RunId is { } run) { run.TryWriteBytes(bytes[offset..]); offset += 16; }
        if (origin.Generation is { } generation) { generation.TryWriteBytes(bytes[offset..]); offset += 16; }
        if (origin.CausationId is { } causation) { causation.TryWriteBytes(bytes[offset..]); offset += 16; }
        return Convert.ToBase64String(bytes[..offset]);
    }
    public static MessageOrigin Deserialize(string wire)
    {
        if (wire.Length is 0 or > 768) throw new InvalidDataException("Invalid V2 origin length.");
        foreach (var c in wire) if (char.IsWhiteSpace(c)) throw new InvalidDataException("Whitespace in V2 origin.");
        Span<byte> bytes = stackalloc byte[576];
        if (!Convert.TryFromBase64String(wire, bytes, out var count) || count < 15 || !bytes[..4].SequenceEqual("IMO\x02"u8))
            throw new InvalidDataException("Invalid V2 origin version or encoding.");
        var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes[12..]); var flags = bytes[14];
        if ((flags & ~7) != 0 || length is 0 or > 512 || count != 15 + length + 16 * BitOperations.PopCount((uint)flags))
            throw new InvalidDataException("Invalid V2 origin shape.");
        try
        {
            var at = new DateTimeOffset(BinaryPrimitives.ReadInt64LittleEndian(bytes[4..]), TimeSpan.Zero);
            var domain = Utf8.GetString(bytes.Slice(15, length)); var offset = 15 + length;
            Guid? run = null, generation = null, causation = null;
            if ((flags & 1) != 0) { run = new Guid(bytes.Slice(offset, 16)); offset += 16; }
            if ((flags & 2) != 0) { generation = new Guid(bytes.Slice(offset, 16)); offset += 16; }
            if ((flags & 4) != 0) causation = new Guid(bytes.Slice(offset, 16));
            var result = new MessageOrigin(at, domain, run, generation, causation);
            result.RememberBinary(wire);
            return result;
        }
        catch (ArgumentException error) { throw new InvalidDataException("Invalid V2 origin fields.", error); }
    }
}
