using System.Collections.ObjectModel;
using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;

/// <summary>Immutable list of databases available to the backup workflow.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record DatabaseNamesReadModel
{
    [Key(0)]
    public IReadOnlyList<string> Names { get; init; }

    public DatabaseNamesReadModel() : this(Array.Empty<string>()) { }

    [SerializationConstructor]
    public DatabaseNamesReadModel(IReadOnlyList<string>? names)
    {
        if (names is null || names.Count == 0)
        {
            Names = Array.Empty<string>();
            return;
        }

        var values = new string[names.Count];
        for (var index = 0; index < names.Count; index++)
            values[index] = names[index];
        Names = new ReadOnlyCollection<string>(values);
    }
}
