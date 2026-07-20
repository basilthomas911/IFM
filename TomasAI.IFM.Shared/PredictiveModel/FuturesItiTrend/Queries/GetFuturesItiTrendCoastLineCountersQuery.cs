using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve futures ITI trend coastline counters based on specified parameters.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class GetFuturesItiTrendCoastLineCountersQuery : IQuery<FuturesItiTrendCoastLineCountersReadModel>
{
    [IgnoreMember] public const string Actor = "FuturesItiTrendQuery";
    [IgnoreMember] public const string Verb = "GetFuturesItiTrendCoastLineCounters";
    [IgnoreMember] public const int ErrorId = 21003;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string? QueryParams { get; set; }

    [Key(2)]
    public string ContractId { get; set; }

    [Key(3)]
    public DateOnly ValueDate { get; set; }

    [Key(4)]
    public string Symbol { get; set; }

    [Key(5)]
    public double PredictedTrendDelta { get; set; }

    public GetFuturesItiTrendCoastLineCountersQuery() { }

    public GetFuturesItiTrendCoastLineCountersQuery(string contractId, DateOnly valueDate, string symbol, double predictedTrendDelta)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        Symbol = symbol ?? string.Empty;
        PredictedTrendDelta = predictedTrendDelta;
        ErrorCode = ErrorId;
        QueryParams = $"contractId={ContractId}&valueDate={ValueDate:yyyy-MM-dd}&symbol={Symbol}&predictedTrendDelta={PredictedTrendDelta}";
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetFuturesItiTrendCoastLineCountersQuery(
        ActorSubject subject,          // Key(0)
        IActorEntityId entityId,       // Key(1)
        string contractId,             // Key(2)
        DateOnly valueDate,            // Key(3)
        string symbol,                 // Key(4)
        double predictedTrendDelta)    // Key(5)
    {
        Subject = subject;
        EntityId = entityId;
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        Symbol = symbol ?? string.Empty;
        PredictedTrendDelta = predictedTrendDelta;
        ErrorCode = ErrorId;
    }
}
