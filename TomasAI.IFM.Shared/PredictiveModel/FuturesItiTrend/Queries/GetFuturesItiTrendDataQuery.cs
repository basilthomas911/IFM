using MessagePack;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve futures ITI trend data for a symbol and date range.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetFuturesItiTrendDataQuery : IQuery<FuturesItiTrendDeltaDataReadModel[]>
{
    [IgnoreMember] public const string Actor = "FuturesItiTrendDataQuery";
    [IgnoreMember] public const string Verb = "GetFuturesItiTrendData";
    [IgnoreMember] public const int ErrorId = 21001;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public string Symbol { get; init; } = string.Empty;

    [Key(3)]
    public DateTime StartDate { get; init; }

    [Key(4)]
    public DateTime EndDate { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetFuturesItiTrendDataQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetFuturesItiTrendDataQuery(string symbol, DateTime startDate, DateTime endDate)
    {
        Symbol = symbol ?? string.Empty;
        StartDate = startDate;
        EndDate = endDate;
        EntityId = new GetFuturesItiTrendDataParameter(symbol, startDate, endDate);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesItiTrendDataQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        string symbol,              // Key(2)
        DateTime startDate,         // Key(3)
        DateTime endDate)           // Key(4)
    {
        Subject = subject;
        EntityId = new GetFuturesItiTrendDataParameter(symbol, startDate, endDate);
        Symbol = symbol ?? string.Empty;
        StartDate = startDate;
        EndDate = endDate;
        ErrorCode = ErrorId;
    }
}

