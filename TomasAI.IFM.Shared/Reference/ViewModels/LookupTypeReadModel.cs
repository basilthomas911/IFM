using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Reference.ViewModels;

/// <summary>
/// MessagePack-serializable view model describing a lookup type definition (name, short code, ordering, description, and creation metadata).
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived identifiers
/// are excluded via <see cref="IgnoreMemberAttribute"/> and <see cref="JsonIgnoreAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeReadModel
{
    /// <summary>Human-readable name of the lookup type.</summary>
    [Key(0)]
    public string LookupTypeName { get; init; }

    /// <summary>Short code or mnemonic representing the lookup type.</summary>
    [Key(1)]
    public string ShortCode { get; init; }

    /// <summary>Ordering/grouping identifier for presentation or processing.</summary>
    [Key(2)]
    public int OrderId { get; init; }

    /// <summary>Descriptive text providing context for the lookup type.</summary>
    [Key(3)]
    public string Description { get; init; }

    /// <summary>UTC timestamp when this lookup type definition was created.</summary>
    [Key(4)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or system that created this lookup type.</summary>
    [Key(5)]
    public string CreatedBy { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers; initializes string fields to empty and numeric fields to defaults.
    /// </summary>
    public LookupTypeReadModel()
    {
        LookupTypeName = string.Empty;
        ShortCode = string.Empty;
        OrderId = 0;
        Description = string.Empty;
        CreatedOn = DateTime.UtcNow;
        CreatedBy = string.Empty;
    }

    /// <summary>
    /// Creates a new lookup type view model instance.
    /// </summary>
    public LookupTypeReadModel(
        string lookupTypeName,
        string shortCode,
        int orderId,
        string description,
        DateTime createdOn,
        string createdBy)
    {
        LookupTypeName = lookupTypeName;
        ShortCode = shortCode;
        OrderId = orderId;
        Description = description;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }

    /// <summary>Derived identifier combining name and order (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public LookupTypeId Id => new(LookupTypeName, OrderId);

    /// <summary>Derived short code identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public LookupTypeShortCode ShortCodeId => new(LookupTypeName, ShortCode);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => !string.IsNullOrEmpty(LookupTypeName) && !string.IsNullOrEmpty(ShortCode);

    /// <summary>
    /// Returns a compact JSON representation of the lookup type.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);

    /// <summary>
    /// Provides a default (empty) lookup type view model instance.
    /// </summary>
    public static LookupTypeReadModel Default => new(
        lookupTypeName: string.Empty,
        shortCode: string.Empty,
        orderId: -1,
        description: string.Empty,
        createdOn: DateTime.UtcNow,
        createdBy: string.Empty);
}