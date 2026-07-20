using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Commands;

/// <summary>
/// Command to train a Futures ITI Trend classification model for a symbol over a historical date range,
/// using pre-computed data statistics.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.FuturesItiTrendBoundedContext"/> with error code 20018.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TrainFuturesItiTrendClassModelCommand : ICommand<FuturesItiTrendEntityId>
{
    public const string Actor = "FuturesItiTrendCommand";
    public const string Verb = "TrainClassModel";

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesItiTrendEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The futures symbol whose trend classification model is trained.</summary>
    [Key(6)]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>The value (as-of) date for the training context.</summary>
    [Key(7)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Start date (inclusive) of the historical training window.</summary>
    [Key(8)]
    public DateOnly StartDate { get; init; }

    /// <summary>End date (inclusive) of the historical training window.</summary>
    [Key(9)]
    public DateOnly EndDate { get; init; }

    /// <summary>Aggregated statistics describing the training data set.</summary>
    [Key(10)]
    public FuturesItiTrendModelDataStatistics Statistics { get; init; }

    /// <summary>Parameterless constructor required for MessagePack deserialization.</summary>
    public TrainFuturesItiTrendClassModelCommand() { }

    /// <summary>
    /// Creates a new command to train a Futures ITI Trend classification model.
    /// </summary>
    /// <param name="symbol">Target futures symbol (null becomes empty).</param>
    /// <param name="valueDate">As-of date for the model.</param>
    /// <param name="startDate">Start of historical range.</param>
    /// <param name="endDate">End of historical range.</param>
    /// <param name="statistics">Pre-computed data statistics.</param>
    public TrainFuturesItiTrendClassModelCommand(
        string symbol,
        DateOnly valueDate,
        DateOnly startDate,
        DateOnly endDate,
        FuturesItiTrendModelDataStatistics statistics)
    {
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        StartDate = startDate;
        EndDate = endDate;
        Statistics = statistics;

        EntityId = new FuturesItiTrendEntityId(Symbol, ValueDate);
        RouteTo = BoundedContextName.FuturesItiTrendBoundedContext;
        ErrorCode = 20018;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public TrainFuturesItiTrendClassModelCommand(
        Guid commandId,                   // Key(0)
        ActorSubject subject,             // Key(1)
        bool postEvents,                  // Key(2)
        FuturesItiTrendEntityId entityId, // Key(3)
        int errorCode,                    // Key(4)
        BoundedContextName routeTo,       // Key(5)
        string symbol,                    // Key(6)
        DateOnly valueDate,               // Key(7)
        DateOnly startDate,               // Key(8)
        DateOnly endDate,                 // Key(9)
        FuturesItiTrendModelDataStatistics statistics) // Key(10)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Symbol = symbol;
        ValueDate = valueDate;
        StartDate = startDate;
        EndDate = endDate;
        Statistics = statistics;
    }
}