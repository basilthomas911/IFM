using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Commands;

/// <summary>
/// Command to change (update) an existing strike price volatility definition.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.StrikePriceVolatilityBoundedContext"/> with error code 8005.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ChangeStrikePriceVolatilityCommand
    : ICommand<StrikePriceVolatilityId>
{
    public const string Actor = "StrikePriceVolatilityCommand";
    public const string Verb = "Change";
    public const int ErrorId = 8005;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public StrikePriceVolatilityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The original identifier of the strike price volatility entry being updated.</summary>
    [Key(6)]
    public StrikePriceVolatilityId OriginalId { get; init; }

    /// <summary>The updated strike price volatility payload.</summary>
    [Key(7)]
    public StrikePriceVolatilityReadModel StrikePriceVolatility { get; init; }

    /// <summary>True to overwrite existing data; otherwise false.</summary>
    [Key(8)]
    public bool Overwrite { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ChangeStrikePriceVolatilityCommand() { }

    /// <summary>
    /// Creates a new command to update strike price volatility details.
    /// </summary>
    /// <param name="originalId">Original strike price volatility identifier.</param>
    /// <param name="strikePriceVolatility">Updated strike price volatility view model (cannot be null).</param>
    /// <param name="overwrite">Set true to overwrite existing data.</param>
    public ChangeStrikePriceVolatilityCommand(
        StrikePriceVolatilityId originalId,
        StrikePriceVolatilityReadModel strikePriceVolatility,
        bool overwrite = false)
    {
        OriginalId = originalId;
        StrikePriceVolatility = strikePriceVolatility ?? throw new ArgumentNullException(nameof(strikePriceVolatility));
        Overwrite = overwrite;

        EntityId = OriginalId;
        RouteTo = BoundedContextName.StrikePriceVolatilityBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> values).
    /// </summary>
    [SerializationConstructor]
    public ChangeStrikePriceVolatilityCommand(
        Guid commandId,                     // Key(0)
        ActorSubject subject,               // Key(1)
        bool postEvents,                    // Key(2)
        StrikePriceVolatilityId entityId,   // Key(3)
        int errorCode,                      // Key(4)
        BoundedContextName routeTo,         // Key(5)
        StrikePriceVolatilityId originalId, // Key(6)
        StrikePriceVolatilityReadModel strikePriceVolatility, // Key(7)
        bool overwrite)                     // Key(8)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        OriginalId = originalId;
        StrikePriceVolatility = strikePriceVolatility;
        Overwrite = overwrite;
    }
}