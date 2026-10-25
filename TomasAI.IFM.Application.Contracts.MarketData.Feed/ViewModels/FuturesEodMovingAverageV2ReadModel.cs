using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

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
