using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

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
