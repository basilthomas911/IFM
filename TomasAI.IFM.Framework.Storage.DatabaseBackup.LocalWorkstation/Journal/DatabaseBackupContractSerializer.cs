using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Journal;

internal static class DatabaseBackupContractSerializer
{
    static readonly IReadOnlyDictionary<string, Type> EventTypes = typeof(DatabaseBackupEventContract).Assembly
        .GetTypes()
        .Where(static type => !type.IsAbstract && typeof(DatabaseBackupEventContract).IsAssignableFrom(type))
        .ToDictionary(static type => type.FullName!, StringComparer.Ordinal);

    public static (string TypeName, string Payload, string Hash) Serialize(DatabaseBackupEventContract @event)
    {
        var typeName = @event.GetType().FullName
            ?? throw new InvalidOperationException("DatabaseBackup event type has no stable name.");
        if (!EventTypes.ContainsKey(typeName))
            throw new InvalidOperationException($"DatabaseBackup event type '{typeName}' is not allowlisted.");
        var payload = Convert.ToBase64String(MessagePackSerializer.Serialize(@event.GetType(), @event));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(typeName + "\n" + payload)));
        return (typeName, payload, hash);
    }

    public static DatabaseBackupEventContract Deserialize(string typeName, string payload)
    {
        if (!EventTypes.TryGetValue(typeName, out var type))
            throw new InvalidOperationException($"Stored DatabaseBackup event type '{typeName}' is not allowlisted.");
        return (DatabaseBackupEventContract?)MessagePackSerializer.Deserialize(type, Convert.FromBase64String(payload))
            ?? throw new InvalidOperationException($"Stored DatabaseBackup event '{typeName}' is invalid.");
    }

    public static string DefinitionHash(DatabaseBackupEventContract @event)
    {
        var definition = string.Join('|',
            @event.Source.OperationId.Value,
            (short)@event.Source.Source,
            (short)@event.Source.OperationKind,
            @event.Source.ProtectionSetId.Value,
            @event.Source.BackupSetId?.Value.ToString() ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition)));
    }
}
