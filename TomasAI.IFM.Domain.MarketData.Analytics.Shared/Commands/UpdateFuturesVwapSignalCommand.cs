using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

/// <summary>Requests one event-sourced live futures trade contribution.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record UpdateFuturesVwapSignalCommand : ICommand<FuturesVwapSignalEntityId>
{

    /// <summary>Creates an empty command for serialization.</summary>
    public UpdateFuturesVwapSignalCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="observation">The Observation field.</param>
    /// <param name="configuration">The Configuration field.</param>
    [SerializationConstructor]
    public UpdateFuturesVwapSignalCommand(Guid commandId, ActorSubject subject, bool postEvents, FuturesVwapSignalEntityId entityId, int errorCode, BoundedContextName routeTo, FuturesVwapTradeObservation observation, FuturesVwapConfiguration configuration)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Observation = observation;
        Configuration = configuration;
    }
    public const string Actor = "FuturesVwapSignalCommand";
    public const string Verb = "Update";
    public const int ErrorId = 26400;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesVwapSignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesVwapSignalBoundedContext;
    [Key(6)] public FuturesVwapTradeObservation Observation { get; init; } = new();
    [Key(7)] public FuturesVwapConfiguration Configuration { get; init; } = FuturesVwapConfiguration.Standard;
    [IgnoreMember] public string CommandName => nameof(UpdateFuturesVwapSignalCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}
