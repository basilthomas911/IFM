using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Unique identifier for a market data feed represented by a single integer value.
/// </summary>
/// <remarks>
/// MessagePack serializable. Implements <see cref="IActorEntityId"/> with a dot-separated format; since this
/// identifier has a single component, the formatted value is just the integer as a string.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FeedId : IActorEntityId
{
    /// <summary>
    /// The integer value representing the unique feed identifier.
    /// </summary>
    [Key(0)]
    public int Value { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FeedId() { }

    /// <summary>
    /// Creates a new <see cref="FeedId"/> with the specified value.
    /// </summary>
    /// <param name="value">The unique feed identifier value.</param>
    public FeedId(int value)
    {
        Value = value;
    }

    /// <summary>
    /// Formats the identifier into a stable string key. For a single-component identifier, this is the value itself.
    /// </summary>
    public string Format() => Value.ToString();

    [IgnoreMember]
    public bool IsValid
        => Value != 0;
}
