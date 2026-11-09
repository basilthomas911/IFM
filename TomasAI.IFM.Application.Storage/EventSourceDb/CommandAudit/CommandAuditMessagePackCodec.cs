using System.Buffers;
using System.Security.Cryptography;
using MessagePack;
using MessagePack.Resolvers;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

/// <summary>Versioned command-log codec. Command audit payloads are deliberately never compressed.</summary>
public sealed class CommandAuditMessagePackCodec
{
    public const int CurrentVersion = 1;

    static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithResolver(ContractlessStandardResolver.Instance)
        .WithCompression(MessagePackCompression.None);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
            SHA256.HashData(bytes), TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(command));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public object Deserialize(Type commandType, ReadOnlyMemory<byte> payload, short format, int version,
        TomasAI.IFM.Shared.EventModelActor.MessageOrigin? origin = null)
    {
        ArgumentNullException.ThrowIfNull(commandType);
        if (!typeof(ICommand).IsAssignableFrom(commandType))
            throw new ArgumentException("The requested type is not a command.", nameof(commandType));
        if (format != (short)CommandAuditPayloadFormat.MessagePack || version != CurrentVersion)
            throw new InvalidDataException($"Unsupported command audit payload format/version {format}/{version}.");
        if (payload.IsEmpty) throw new InvalidDataException("Command audit payload is empty.");
        var value = MessagePackSerializer.Deserialize(commandType, payload, Options)
            ?? throw new InvalidDataException("Command audit payload produced no command.");
        return TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Restore(value, origin);
    }

    public bool Matches(ICommand command, ReadOnlySpan<byte> expectedHash)
    {
        if (expectedHash.Length != SHA256.HashSizeInBytes) return false;
        var candidate = Serialize(command);
        return CryptographicOperations.FixedTimeEquals(candidate.Sha256, expectedHash);
    }

    /// <summary>Checks exact stored fields, permitting only newly appended null fields.</summary>
    public bool MatchesStoredEncoding(ICommand command, ReadOnlyMemory<byte> stored)
    {
        var rewritten = Serialize(command).Bytes;
        if (stored.Span.SequenceEqual(rewritten)) return true;
        if (command is not IAppendOnlyCommandRetryIdentity) return false;
        var originalReader = new MessagePackReader(stored);
        var currentReader = new MessagePackReader(rewritten);
        var originalCount = originalReader.ReadArrayHeader();
        var currentCount = currentReader.ReadArrayHeader();
        if (originalCount > currentCount) return false;
        for (var index = 0; index < originalCount; index++)
        {
            var before = originalReader.ReadRaw();
            var after = currentReader.ReadRaw();
            if (!before.IsSingleSegment || !after.IsSingleSegment || !before.FirstSpan.SequenceEqual(after.FirstSpan)) return false;
        }
        for (var index = originalCount; index < currentCount; index++)
            if (!currentReader.TryReadNil()) return false;
        return originalReader.End && currentReader.End;
    }
}
