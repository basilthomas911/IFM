using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.SystemAdmin;

/// <summary>
/// MessagePack-serializable identifier for a database backup operation, composed of the database name.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot-separated components; with a single
/// component it resolves to "DatabaseName".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record DatabaseBackupId(
    [property: Key(0)] string DatabaseName) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to an empty database name.
    /// </summary>
    public DatabaseBackupId() : this(string.Empty) { }

    /// <summary>
    /// Formats the identifier as a dot-separated key.
    /// </summary>
    /// <returns>Formatted string (e.g., "MyDatabase").</returns>
    public string Format() => DatabaseName.ToString();
}
