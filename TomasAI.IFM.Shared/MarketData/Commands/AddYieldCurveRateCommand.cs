using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.Commands;

/// <summary>
/// Command to add (or overwrite) a yield curve rate definition.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.YieldCurveRateBoundedContext"/> with error code 6003.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddYieldCurveRateCommand : ICommand<YieldCurveRateEntityId>
{
    public const string Actor = "YieldCurveRateCommand";
    public const string Verb = "Add";
    public const int ErrorId = 6003;

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

    /// <summary>The yield curve rate payload to add or update.</summary>
    [Key(6)]
    public YieldCurveRateReadModel YieldCurveRate { get; init; }

    /// <summary>True to overwrite an existing rate with the same entity identifier; otherwise false.</summary>
    [Key(7)]
    public bool Overwrite { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public AddYieldCurveRateCommand() { }

    /// <summary>
    /// Creates a new command to add a yield curve rate.
    /// </summary>
    /// <param name="yieldCurveRate">Yield curve rate view model (cannot be null).</param>
    /// <param name="overwrite">Set true to overwrite an existing rate.</param>
    public AddYieldCurveRateCommand(YieldCurveRateReadModel yieldCurveRate, bool overwrite = false)
    {
        YieldCurveRate = yieldCurveRate ?? throw new ArgumentNullException(nameof(yieldCurveRate));
        Overwrite = overwrite;

        EntityId = YieldCurveRate.EntityId;
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.YieldCurveRateBoundedContext;
    }

    [SerializationConstructor]
    public AddYieldCurveRateCommand(
        Guid commandId,                      // Key(0)
        ActorSubject subject,                // Key(1)
        bool postEvents,                     // Key(2)
        YieldCurveRateEntityId entityId,     // Key(3)
        int errorCode,                       // Key(4)
        BoundedContextName routeTo,          // Key(5)
        YieldCurveRateReadModel yieldCurveRate, // Key(6)
        bool overwrite)                      // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        YieldCurveRate = yieldCurveRate;
        Overwrite = overwrite;
    }
}