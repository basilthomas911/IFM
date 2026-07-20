using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve futures ITI trend data.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesItiTrendDataParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string Symbol { get; init; } = string.Empty;
    [Key(1)] public DateTime StartDate { get; init; }
    [Key(2)] public DateTime EndDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesItiTrendDataParameter() { }

    [SerializationConstructor]
    public GetFuturesItiTrendDataParameter(string symbol, DateTime startDate, DateTime endDate)
    {
        Symbol = symbol;
        StartDate = startDate;
        EndDate = endDate;
        QueryParams = $"symbol={Symbol}&startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{Symbol}";
}
