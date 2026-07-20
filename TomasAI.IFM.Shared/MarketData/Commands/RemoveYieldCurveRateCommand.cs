using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.Commands;

/// <summary>
/// Command to remove a yield curve rate for a specific value (trading) date.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0–5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.YieldCurveRateBoundedContext"/> with error code 6009.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record RemoveYieldCurveRateCommand : ICommand<YieldCurveRateEntityId>
{
    public const string Actor = "YieldCurveRateCommand";
    public const string Verb = "Remove";
    public const int ErrorId = 6009;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public YieldCurveRateEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The value (trading) date of the yield curve rate to remove.</summary>
    [Key(6)]
    public DateOnly ValueDate { get; init; }

    /// <summary>True to force/overwrite removal where applicable; otherwise false.</summary>
    [Key(7)]
    public bool Overwrite { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public RemoveYieldCurveRateCommand() { }

    /// <summary>
    /// Creates a new command to remove a yield curve rate for the specified value date.
    /// </summary>
    /// <param name="valueDate">The target value (trading) date.</param>
    /// <param name="overwrite">Set true to force/overwrite related persisted data.</param>
    public RemoveYieldCurveRateCommand(DateOnly valueDate, bool overwrite = false)
    {
        ValueDate = valueDate;
        Overwrite = overwrite;

        EntityId = new YieldCurveRateEntityId(valueDate.Year);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.YieldCurveRateBoundedContext;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public RemoveYieldCurveRateCommand(
        Guid commandId,                  // Key(0)
        ActorSubject subject,            // Key(1)
        bool postEvents,                 // Key(2)
        YieldCurveRateEntityId entityId, // Key(3)
        int errorCode,                   // Key(4)
        BoundedContextName routeTo,      // Key(5)
        DateOnly valueDate,              // Key(6)
        bool overwrite)                  // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ValueDate = valueDate;
        Overwrite = overwrite;
    }
}