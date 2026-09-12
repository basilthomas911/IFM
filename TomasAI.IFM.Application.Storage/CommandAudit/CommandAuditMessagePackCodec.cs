using System.Buffers;
using System.Security.Cryptography;
using MessagePack;
using MessagePack.Resolvers;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.CommandAudit;

/// <summary>Versioned command-log codec. Command audit payloads are deliberately never compressed.</summary>
public sealed class CommandAuditMessagePackCodec
{
    public const int CurrentVersion = 1;

    static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithResolver(ContractlessStandardResolver.Instance)
        .WithCompression(MessagePackCompression.None);

    public CommandAuditPayload Serialize(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var buffer = new ArrayBufferWriter<byte>();
        MessagePackSerializer.Serialize(command.GetType(), buffer, command, Options);
        var bytes = buffer.WrittenSpan.ToArray();
        return new CommandAuditPayload(
            CommandAuditPayloadFormat.MessagePack,
            CurrentVersion,
            bytes,
            SHA256.HashData(bytes));
    }

    public object Deserialize(Type commandType, ReadOnlyMemory<byte> payload, short format, int version)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        if (!typeof(ICommand).IsAssignableFrom(commandType))
            throw new ArgumentException("The requested type is not a command.", nameof(commandType));
        if (format != (short)CommandAuditPayloadFormat.MessagePack || version != CurrentVersion)
            throw new InvalidDataException($"Unsupported command audit payload format/version {format}/{version}.");
        if (payload.IsEmpty) throw new InvalidDataException("Command audit payload is empty.");
        return MessagePackSerializer.Deserialize(commandType, payload, Options)
            ?? throw new InvalidDataException("Command audit payload produced no command.");
    }

    public bool Matches(ICommand command, ReadOnlySpan<byte> expectedHash)
    {
        if (expectedHash.Length != SHA256.HashSizeInBytes) return false;
        var candidate = Serialize(command);
        return CryptographicOperations.FixedTimeEquals(candidate.Sha256, expectedHash);
    }
}

public sealed record CommandAuditPayload(
    CommandAuditPayloadFormat Format,
    int Version,
    byte[] Bytes,
    byte[] Sha256);
