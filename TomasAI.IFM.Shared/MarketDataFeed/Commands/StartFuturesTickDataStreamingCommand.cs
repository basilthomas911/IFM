using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to start streaming futures tick data for a specific contract and value date.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands (base command keys 0�5).
/// Custom properties begin at key index 6. Routes to <see cref="BoundedContextName.FuturesTickDataBoundedContext"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StartFuturesTickDataStreamingCommand : ICommand<FuturesDataId>
{
    public const string Actor = "FuturesTickDataCommand";
    public const string Verb = "StartStreaming";
    public const int ErrorId = 5004;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesDataId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The futures contract for which tick data streaming is started.</summary>
    [Key(6)]
    public FuturesContractV2ReadModel Contract { get; init; }

    /// <summary>The value (trading) date of the tick data stream.</summary>
    [Key(7)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Indicates whether an existing stream should be reset before starting a new one.
    /// </summary>
    [Key(8)]
    public bool ResetStream { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StartFuturesTickDataStreamingCommand() { }

    /// <summary>
    /// Creates a new command to start streaming futures tick data.
    /// </summary>
    /// <param name="contract">Futures contract (cannot be null).</param>
    /// <param name="valueDate">Value date of the stream.</param>
    /// <param name="resetStream">True to reset any existing stream; otherwise false.</param>
    public StartFuturesTickDataStreamingCommand(
        FuturesContractV2ReadModel contract,
        DateOnly valueDate,
        bool resetStream)
    {
        Contract = contract ?? throw new ArgumentNullException(nameof(contract));
        ValueDate = valueDate;
        ResetStream = resetStream;

        EntityId = new FuturesDataId(Contract.ContractId ?? string.Empty, ValueDate);
        RouteTo = BoundedContextName.FuturesTickDataBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters align with key order 0�8.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Indicates whether to publish resulting events (key 2).</param>
    /// <param name="entityId">Futures data entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="contract">The futures contract payload (key 6).</param>
    /// <param name="valueDate">The value date (key 7).</param>
    /// <param name="resetStream">Whether to reset the stream (key 8).</param>
    [SerializationConstructor]
    public StartFuturesTickDataStreamingCommand(
        Guid commandId,                              // Key(0)
        ActorSubject subject,                        // Key(1)
        bool postEvents,                             // Key(2)
        FuturesDataId entityId,                      // Key(3)
        int errorCode,                               // Key(4)
        BoundedContextName routeTo,                  // Key(5)
        FuturesContractV2ReadModel contract,         // Key(6)
        DateOnly valueDate,                          // Key(7)
        bool resetStream)                            // Key(8)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Contract = contract;
        ValueDate = valueDate;
        ResetStream = resetStream;
    }
}