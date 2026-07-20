using System.Collections.Immutable;
using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// Represents a view model for a fund, including identity, metadata, and related orders.
/// </summary>
/// <remarks>
/// This type is MessagePack-serializable using stable numeric keys for compact and version-tolerant payloads.
/// Computed members are excluded from the MessagePack payload.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundReadModel
{
    /// <summary>
    /// Gets the unique fund identifier.
    /// </summary>
    [Key(0)]
    public int FundId { get; init; }

    /// <summary>
    /// Gets the fund name.
    /// </summary>
    [Key(1)]
    public string Name { get; init; }

    /// <summary>
    /// Gets the fund description.
    /// </summary>
    [Key(2)]
    public string Description { get; init; }

    /// <summary>
    /// Gets the current fund balance.
    /// </summary>
    [Key(3)]
    public decimal Balance { get; init; }

    /// <summary>
    /// Gets a value indicating whether this fund is a production fund.
    /// </summary>
    [Key(4)]
    public bool IsProduction { get; init; }

    /// <summary>
    /// Gets the timestamp when the fund was created.
    /// </summary>
    [Key(5)]
    public DateTime CreatedOn { get; init; }

    /// <summary>
    /// Gets the user who created the fund.
    /// </summary>
    [Key(6)]
    public string CreatedBy { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FundReadModel"/> record.
    /// </summary>
    public FundReadModel(
        int fundId,
        string name,
        string description,
        decimal balance,
        bool isProduction,
        DateTime createdOn,
        string createdBy)
    {
        FundId = fundId;
        Name = name;
        Description = description;
        Balance = balance;
        IsProduction = isProduction;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }

    /// <summary>
    /// Indicates whether the fund model contains valid data.
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => FundId > 0 && !string.IsNullOrEmpty(Name);

    /// <summary>
    /// Gets the strongly-typed fund identifier.
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FundId Id => new(FundId);

    [JsonIgnore]
    [IgnoreMember]
    private List<FundOrderReadModel>? _orders;

    /// <summary>
    /// Gets the collection of fund orders.
    /// </summary>
    /// <remarks>
    /// Excluded from MessagePack payload, but included in JSON for UI/API scenarios.
    /// </remarks>
    [JsonProperty]
    [IgnoreMember]
    public ImmutableArray<FundOrderReadModel> Orders => _orders is null ? [] : [.. _orders];

    /// <summary>
    /// Adds a fund order to the collection.
    /// </summary>
    /// <param name="fundOrder">The fund order to add.</param>
    public void Add(FundOrderReadModel fundOrder)
    {
        ArgumentNullException.ThrowIfNull(fundOrder);
        _orders ??= [];
        _orders.Add(fundOrder);
    }

    /// <summary>
    /// Adds a range of fund orders to the collection.
    /// </summary>
    /// <param name="fundOrders">The fund orders to add.</param>
    public void AddRange(IEnumerable<FundOrderReadModel> fundOrders)
    {
        ArgumentNullException.ThrowIfNull(fundOrders);
        _orders ??= [];
        _orders.AddRange(fundOrders);
    }

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}