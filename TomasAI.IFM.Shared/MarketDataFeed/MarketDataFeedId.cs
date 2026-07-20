using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Unique identifier for a market data feed associated with a specific value (trading) date.
/// </summary>
/// <remarks>
/// MessagePack serializable. Implements <see cref="IActorEntityId"/> with a dot-separated format. As this
/// identifier has a single component (ValueDate), the formatted key is the date itself (yyyy-MM-dd).
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record MarketDataFeedId : IActorEntityId
{
    /// <summary>The value (trading) date for the market data feed.</summary>
    [Key(0)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public MarketDataFeedId() { }

    /// <summary>
    /// Creates a new market data feed identifier for the specified value date.
    /// </summary>
    /// <param name="valueDate">Trading date.</param>
    public MarketDataFeedId(DateOnly valueDate)
    {
        ValueDate = valueDate;
    }

    /// <summary>
    /// Formats the identifier into a stable string key (yyyy-MM-dd).
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[48], $"{ValueDate:yyyy-MM-dd}");

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
