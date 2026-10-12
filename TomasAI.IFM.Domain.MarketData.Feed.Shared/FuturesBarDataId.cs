using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>
/// Unique identifier for a futures bar (OHLCV) data snapshot composed of a contract identifier,
/// a symbol root, and a value (trading) date.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a
/// dot-separated key format: ContractId.Symbol.yyyy-MM-dd. Provides formatting and JSON helpers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesBarDataId : IActorEntityId
{
    /// <summary>Full futures contract identifier (root + month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Symbol root (e.g., ES, NQ, CL).</summary>
    [Key(1)]
    public string Symbol { get; init; }

    /// <summary>Value (trading) date of the bar data.</summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesBarDataId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesBarDataId"/>.
    /// </summary>
    /// <param name="contractId">Full futures contract identifier.</param>
    /// <param name="symbol">Symbol root.</param>
    /// <param name="valueDate">Trading date.</param>
    public FuturesBarDataId(string contractId, string symbol, DateOnly valueDate)
    {
        ContractId = contractId;
        Symbol = symbol;
        ValueDate = valueDate;
    }

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.Symbol.yyyy-MM-dd
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{Symbol}.{ValueDate:yyyy-MM-dd}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
