using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable model for a single moving average associated with a symbol.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/> with a dot-separated key "Symbol.MovingAverage".
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys and
/// a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesEodMovingAverageReadModel : IActorEntityId
{
    /// <summary>Underlying futures symbol.</summary>
    [Key(0)]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Computed moving average value.</summary>
    [Key(1)]
    public double MovingAverage { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public FuturesEodMovingAverageReadModel() { }

    /// <summary>
    /// Full constructor to create a single moving average snapshot.
    /// </summary>
    public FuturesEodMovingAverageReadModel(string symbol, double movingAverage)
    {
        Symbol = symbol ?? string.Empty;
        MovingAverage = movingAverage;
    }

    /// <summary>
    /// Formats the identifier as a dot-separated key: "Symbol.MovingAverage".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{Symbol}.{MovingAverage}");
}
