using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing end-of-day (EOD) moving averages
/// for a futures symbol on a specific value date.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/> with a dot-separated key:
/// "Symbol.ValueDate.FiftyDMA.TwoHundredDMA" (ValueDate formatted as yyyyMMdd).
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesEodDataMovingAveragesReadModel : IActorEntityId
{
    /// <summary>Underlying futures symbol.</summary>
    [Key(0)]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>As-of (value) date for the moving averages.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>50-day moving average.</summary>
    [Key(2)]
    public decimal FiftyDMA { get; init; }

    /// <summary>200-day moving average.</summary>
    [Key(3)]
    public decimal TwoHundredDMA { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public FuturesEodDataMovingAveragesReadModel() { }

    /// <summary>
    /// Full constructor to create an EOD moving averages snapshot.
    /// </summary>
    public FuturesEodDataMovingAveragesReadModel(string symbol, DateOnly valueDate, decimal fiftyDMA, decimal twoHundredDMA)
    {
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        FiftyDMA = fiftyDMA;
        TwoHundredDMA = twoHundredDMA;
    }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "Symbol.ValueDate.FiftyDMA.TwoHundredDMA".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[96], $"{Symbol}.{ValueDate:yyyyMMdd}.{FiftyDMA}.{TwoHundredDMA}");
}

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

/// <summary>
/// MessagePack-serializable model for a single moving average tied to a specific futures contract and value date.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys and
/// a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesEodMovingAverageV2ReadModel
{
    /// <summary>Full futures contract identifier.</summary>
    [Key(0)]
    public string ContractId { get; init; } = string.Empty;

    /// <summary>As-of (value) date for the moving average.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Computed moving average value.</summary>
    [Key(2)]
    public double MovingAverage { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public FuturesEodMovingAverageV2ReadModel() { }

    /// <summary>
    /// Full constructor to create a contract-scoped moving average snapshot.
    /// </summary>
    /// <param name="contractId">Futures contract identifier.</param>
    /// <param name="valueDate">As-of (value) date.</param>
    /// <param name="movingAverage">Computed moving average.</param>
    public FuturesEodMovingAverageV2ReadModel(string contractId, DateOnly valueDate, double movingAverage)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        MovingAverage = movingAverage;
    }
}
