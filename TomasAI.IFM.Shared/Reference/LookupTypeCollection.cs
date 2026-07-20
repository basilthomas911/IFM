using MessagePack;
using System.Collections;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference;

/// <summary>
/// MessagePack-serializable collection of <see cref="LookupTypeReadModel"/> items.
/// </summary>
/// <remarks>
/// Implements <see cref="ICollection{T}"/> using a private backing list. For MessagePack
/// serialization a private array-backed property is used (key 0). Other runtime-only
/// properties are ignored by the serializer.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class LookupTypeCollection : ICollection<LookupTypeReadModel>
{
    // runtime backing list (ignored by MessagePack)
    [Key(0)]
    public List<LookupTypeReadModel> Items { get; private set; } = [];
    
    /// <summary>
    /// Parameterless constructor for normal usage and serializers.
    /// </summary>
    public LookupTypeCollection() { }

    /// <summary>
    /// MessagePack serialization constructor. MessagePack will deserialize the collection as an array
    /// and pass it here; we copy into the internal list.
    /// </summary>
    [SerializationConstructor]
    public LookupTypeCollection(List<LookupTypeReadModel> items)
    {
        Items = items != null ? [.. items] : [];
    }

    /// <summary>
    /// Number of items in the collection (ignored by MessagePack analyzer).
    /// </summary>
    [IgnoreMember]
    public int Count => Items.Count;

    /// <summary>
    /// Indicates whether collection is read-only (ignored by MessagePack analyzer).
    /// </summary>
    [IgnoreMember]
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public void Add(LookupTypeReadModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Items.Add(item);
    }

    /// <inheritdoc />
    public void Clear() => Items.Clear();

    /// <inheritdoc />
    public bool Contains(LookupTypeReadModel item) => Items.Contains(item);

    /// <inheritdoc />
    public void CopyTo(LookupTypeReadModel[] array, int arrayIndex) => Items.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public bool Remove(LookupTypeReadModel item) => Items.Remove(item);

    /// <inheritdoc />
    public IEnumerator<LookupTypeReadModel> GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}