using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to insert VIX futures end-of-day (EOD) data using the latest VIX futures tick input.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.FuturesEodDataBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0�5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record InsertVixFuturesEodDataCommand : ICommand<FuturesEodDataId>
{
    public const string Actor = "FuturesEodDataCommand";
    public const string Verb = "InsertVix";
    public const int ErrorId = 5005;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesEodDataId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Latest VIX futures tick data used to derive the EOD insertion.
    /// </summary>
    [Key(6)]
    public FuturesTickDataV2ReadModel VixFuturesTickData { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public InsertVixFuturesEodDataCommand() { }

    /// <summary>
    /// Creates a new command to insert VIX futures EOD data.
    /// </summary>
    /// <param name="vixFuturesTickData">The VIX futures tick data input (cannot be null).</param>
    public InsertVixFuturesEodDataCommand(FuturesTickDataV2ReadModel vixFuturesTickData)
    {
        VixFuturesTickData = IsArgumentNull.Set( vixFuturesTickData);

        EntityId = new FuturesEodDataId(VixFuturesTickData.ContractId, VixFuturesTickData.ValueDate);
        ErrorCode = 5005;
        RouteTo = BoundedContextName.FuturesEodDataBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public InsertVixFuturesEodDataCommand(
        Guid commandId,                    // Key(0)
        ActorSubject subject,              // Key(1)
        bool postEvents,                   // Key(2)
        FuturesEodDataId entityId,         // Key(3)
        int errorCode,                     // Key(4)
        BoundedContextName routeTo,        // Key(5)
        FuturesTickDataV2ReadModel data)   // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        VixFuturesTickData = data;
    }
}