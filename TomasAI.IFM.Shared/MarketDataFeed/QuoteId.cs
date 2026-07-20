using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Unique identifier for a quote represented by a single integer value.
/// </summary>
/// <remarks>
/// MessagePack serializable. Implements <see cref="IActorEntityId"/> using a dot-separated pattern; with a single
/// component, the formatted key is the value itself.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record QuoteId : IActorEntityId
{
    /// <summary>The unique quote identifier value.</summary>
    [Key(0)]
    public int Value { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public QuoteId() { }

    /// <summary>
    /// Initializes a new <see cref="QuoteId"/> with the specified value.
    /// </summary>
    /// <param name="value">The unique quote identifier value.</param>
    public QuoteId(int value)
    {
        Value = value;
    }

    /// <summary>
    /// Formats the identifier into a stable string key (single-component).
    /// </summary>
    public string Format() => Value.ToString();

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
