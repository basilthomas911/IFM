using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.Commands;

/// <summary>
/// Command to build a Futures ITI Trend model for a given symbol and date range.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.FuturesItiTrendBoundedContext"/> with error code 20014.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record BuildFuturesItiTrendModelCommand : ICommand<FuturesItiTrendEntityId>
{
    public const string Actor = "FuturesItiTrendCommand";
    public const string Verb = "BuildModel";

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

    /// <summary>The futures symbol for which the model is built.</summary>
    [Key(6)]
    public string Symbol { get; init; }

    /// <summary>The value (as-of) date for the model.</summary>
    [Key(7)]
    public DateOnly ValueDate { get; init; }

    /// <summary>The start date of the historical range.</summary>
    [Key(8)]
    public DateOnly StartDate { get; init; }

    /// <summary>The end date of the historical range.</summary>
    [Key(9)]
    public DateOnly EndDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public BuildFuturesItiTrendModelCommand() { }

    /// <summary>
    /// Creates a new command to build a Futures ITI Trend model.
    /// </summary>
    /// <param name="symbol">Target futures symbol.</param>
    /// <param name="valueDate">As-of date for the model.</param>
    /// <param name="startDate">Start date of the historical range.</param>
    /// <param name="endDate">End date of the historical range.</param>
    public BuildFuturesItiTrendModelCommand(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate)
    {
        Symbol = symbol ?? string.Empty;
        ValueDate = valueDate;
        StartDate = startDate;
        EndDate = endDate;

        EntityId = new FuturesItiTrendEntityId(Symbol, ValueDate);
        RouteTo = BoundedContextName.FuturesItiTrendBoundedContext;
        ErrorCode = 20014;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match Key attributes).
    /// </summary>
    [SerializationConstructor]
    public BuildFuturesItiTrendModelCommand(
        Guid commandId,                       // Key(0)
        ActorSubject subject,                 // Key(1)
        bool postEvents,                      // Key(2)
        FuturesItiTrendEntityId entityId,     // Key(3)
        int errorCode,                        // Key(4)
        BoundedContextName routeTo,           // Key(5)
        string symbol,                        // Key(6)
        DateOnly valueDate,                   // Key(7)
        DateOnly startDate,                   // Key(8)
        DateOnly endDate)                     // Key(9)
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
    }
}