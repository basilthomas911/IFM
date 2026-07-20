using MessagePack;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Queries;

[MessagePackObject(AllowPrivate = true)]
public class GetPredictedTrendDeltaQuery : IQuery<ScalarValue<double>>
{
    [IgnoreMember] public const string Actor = "PreditictiveModelQuery";
    [IgnoreMember] public const string Verb = "GetPredictedTrendDelta";
    [IgnoreMember] public const int ErrorId = 21002;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public FuturesItiTrendDeltaDataReadModel TrendData { get; init; }

    public GetPredictedTrendDeltaQuery() { }

    public GetPredictedTrendDeltaQuery(FuturesItiTrendDeltaDataReadModel trendData)
    {
        TrendData = trendData;
        ErrorCode = ErrorId;
        QueryParams = $"trendData={TrendData}";
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetPredictedTrendDeltaQuery(
        ActorSubject subject,                              // Key(0)
        IActorEntityId entityId,                           // Key(1)
        FuturesItiTrendDeltaDataReadModel trendData)       // Key(2)
    {
        Subject = subject;
        EntityId = entityId;
        TrendData = trendData;
        ErrorCode = ErrorId;
    }
}
