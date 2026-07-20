using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Commands;

/// <summary>
/// Command to add a new strike price volatility definition.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.StrikePriceVolatilityBoundedContext"/> with error code 8004.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddStrikePriceVolatilityCommand : ICommand<StrikePriceVolatilityId>
{
    /// <summary>Actor domain name (excluded from serialization).</summary>
    [IgnoreMember]
    public const string Actor = "StrikePriceVolatilityCommand";

    /// <summary>Verb describing the action (excluded from serialization).</summary>
    [IgnoreMember]
    public const string Verb = "Add";

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

    /// <summary>The strike price volatility view model to add.</summary>
    [Key(6)]
    public StrikePriceVolatilityReadModel StrikePriceVolatility { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public AddStrikePriceVolatilityCommand() { }

    /// <summary>
    /// Creates a new command to add a strike price volatility definition.
    /// </summary>
    /// <param name="strikePriceVolatility">Strike price volatility view model (cannot be null).</param>
    public AddStrikePriceVolatilityCommand(StrikePriceVolatilityReadModel strikePriceVolatility)
    {
        StrikePriceVolatility = strikePriceVolatility ?? throw new ArgumentNullException(nameof(strikePriceVolatility));

        EntityId = StrikePriceVolatility.Id;
        RouteTo = BoundedContextName.StrikePriceVolatilityBoundedContext;
        ErrorCode = 8004;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match Key attributes).
    /// </summary>
    [SerializationConstructor]
    public AddStrikePriceVolatilityCommand(
        Guid commandId,                          // Key(0)
        ActorSubject subject,                    // Key(1)
        bool postEvents,                         // Key(2)
        StrikePriceVolatilityId entityId,        // Key(3)
        int errorCode,                           // Key(4)
        BoundedContextName routeTo,              // Key(5)
        StrikePriceVolatilityReadModel strikePriceVolatility) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        StrikePriceVolatility = strikePriceVolatility;
    }
}