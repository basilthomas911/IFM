using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to start streaming tick data for a specific futures option contract within a given feed.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used across commands. Routes to
/// <see cref="BoundedContextName.FuturesOptionTickDataBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0�5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record StartFuturesOptionTickDataStreamingCommand
    : ICommand<FuturesOptionTickEntityId>
{
    public const string Actor = "FuturesOptionTickDataCommand";
    public const string Verb = "StartStreaming";
    public const int ErrorId = 5004;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesOptionTickEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The futures option contract for which tick data streaming is being started.</summary>
    [Key(6)]
    public FuturesOptionContractReadModel Contract { get; init; }

    /// <summary>The base futures contract associated with the specified option contract.</summary>
    [Key(7)]
    public FuturesContractV2ReadModel BaseContract { get; init; }

    /// <summary>The value date for the futures option contract.</summary>
    [Key(8)]
    public DateOnly ValueDate { get; init; }

    /// <summary>The maturity date for the futures option contract.</summary>
    [Key(9)]
    public DateOnly MaturityDate { get; init; }

    /// <summary>The risk-free rate used for pricing or valuation calculations.</summary>
    [Key(10)]
    public double RiskFreeRate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public StartFuturesOptionTickDataStreamingCommand() { }

    /// <summary>
    /// Creates a new command to start streaming futures option tick data.
    /// </summary>
    /// <param name="feedId">The feed identifier.</param>
    /// <param name="contract">The futures option contract.</param>
    /// <param name="baseContract">The associated base futures contract.</param>
    /// <param name="valueDate">The value date for the option contract.</param>
    /// <param name="maturityDate">The maturity date for the option contract.</param>
    /// <param name="riskFreeRate">Risk-free rate for pricing/valuation.</param>
    public StartFuturesOptionTickDataStreamingCommand(
        FuturesOptionTickEntityId entityId,
        FuturesOptionContractReadModel contract,
        FuturesContractV2ReadModel baseContract,
        DateOnly valueDate,
        DateOnly maturityDate,
        double riskFreeRate)
    {
        EntityId = entityId;
        Contract = contract;
        BaseContract = baseContract;
        ValueDate = valueDate;
        MaturityDate = maturityDate;
        RiskFreeRate = riskFreeRate;
        RouteTo = BoundedContextName.FuturesOptionTickDataBoundedContext;
        ErrorCode = ErrorId;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public StartFuturesOptionTickDataStreamingCommand(
        Guid commandId,                         // Key(0)
        ActorSubject subject,                   // Key(1)
        bool postEvents,                        // Key(2)
        FuturesOptionTickEntityId entityId,     // Key(3)
        int errorCode,                          // Key(4)
        BoundedContextName routeTo,             // Key(5)
        FuturesOptionContractReadModel contract,// Key(7)
        FuturesContractV2ReadModel baseContract,// Key(8)
        DateOnly valueDate,                     // Key(9)
        DateOnly maturityDate,                  // Key(10)
        double riskFreeRate)                    // Key(11)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        Contract = contract;
        BaseContract = baseContract;
        ValueDate = valueDate;
        MaturityDate = maturityDate;
        RiskFreeRate = riskFreeRate;
    }
}