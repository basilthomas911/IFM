using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Unique identifier for a streaming futures tick data session associated with a specific value (trading) date.
/// </summary>
/// <remarks>
/// MessagePack serializable. Implements <see cref="IActorEntityId"/> with a dot-separated key format.
/// With a single component (ValueDate), the formatted key is the date itself: yyyy-MM-dd.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTickDataStreamingId : IActorEntityId
{
    /// <summary>The value (trading) date for the streaming tick data session.</summary>
    [Key(0)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesTickDataStreamingId() { }

    /// <summary>
    /// Creates a new streaming identifier for the specified value date.
    /// </summary>
    /// <param name="valueDate">Trading date.</param>
    public FuturesTickDataStreamingId(DateOnly valueDate)
    {
        ValueDate = valueDate;
    }

    /// <summary>
    /// Formats the identifier into a stable string key (dot-separated pattern): yyyy-MM-dd
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[48], $"{ValueDate:yyyy-MM-dd}");

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
