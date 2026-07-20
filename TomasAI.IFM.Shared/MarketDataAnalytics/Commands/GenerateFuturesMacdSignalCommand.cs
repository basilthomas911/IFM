using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to generate a MACD (Moving Average Convergence Divergence) signal for a futures contract
/// by combining a collection of prior RSI signals.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern adopted by other analytics commands. The command is routed to
/// <see cref="BoundedContextName.FuturesMacdSignalBoundedContext"/>. Properties beyond the base command (keys 0–5)
/// start at index 6 for MessagePack.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GenerateFuturesMacdSignalCommand : ICommand<FuturesMacdSignalEntityId>
{
    public const string Actor = "FuturesMacdSignalCommand";
    public const string Verb = "Generate";
    public const int ErrorId = 20003;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesMacdSignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Identifier describing the target futures contract and value date for the MACD signal generation.
    /// </summary>
    [Key(6)]
    public FuturesMacdSignalId FuturesMacdSignalId { get; init; }

    /// <summary>
    /// Collection of RSI signals used as input factors for computing the MACD signal.
    /// </summary>
    [Key(7)]
    public decimal FuturesPrice { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public GenerateFuturesMacdSignalCommand() { }

    /// <summary>
    /// Creates a new MACD signal generation command for the specified contract/date and RSI signal set.
    /// </summary>
    /// <param name="futuresMacdSignalId">Target MACD signal identifier (contract + value date + timestamp context).</param>
    /// <param name="futuresPrice">The price of the futures contract for which to generate the MACD signal.</param>
    public GenerateFuturesMacdSignalCommand(
        FuturesMacdSignalId futuresMacdSignalId,
        decimal futuresPrice)
    {
        FuturesMacdSignalId = futuresMacdSignalId;
        FuturesPrice = futuresPrice;

        EntityId = futuresMacdSignalId.ToEntityId();
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FuturesMacdSignalBoundedContext;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GenerateFuturesMacdSignalCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FuturesMacdSignalEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FuturesMacdSignalId futuresMacdSignalId,
        decimal futuresPrice)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesMacdSignalId = futuresMacdSignalId;
        FuturesPrice = futuresPrice;
    }
}
