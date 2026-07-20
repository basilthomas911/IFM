using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to insert a full futures end-of-day (EOD) data payload including tick data, contract metadata,
/// today's EOD snapshot, historical EOD range, normal curve, window size, and optional VIX EOD data.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.FuturesEodDataBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0�5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record InsertFuturesEodDataCommand : ICommand<FuturesEodDataId>
{
    public const string Actor = "FuturesEodDataCommand";
    public const string Verb = "Insert";
    public const int ErrorId = 5002;

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

    /// <summary>The value date associated with the data set.</summary>
    [Key(6)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Most recent futures tick data used to augment EOD processing.</summary>
    [Key(7)]
    public FuturesTickDataV2ReadModel FuturesTickData { get; init; }

    /// <summary>Contract metadata for the futures instrument.</summary>
    [Key(8)]
    public FuturesContractV2ReadModel Contract { get; init; }

    /// <summary>Today's EOD data snapshot.</summary>
    [Key(9)]
    public FuturesEodDataV2ReadModel EodDataToday { get; init; }

    /// <summary>Historical EOD data range used for rolling statistics and context.</summary>
    [Key(10)]
    public ICollection<FuturesEodDataV2ReadModel> EodDataRange { get; init; }

    /// <summary>Normal curve data (e.g., yield curve) used during analytics enrichment.</summary>
    [Key(11)]
    public NormalCurveTableReadModel NormCurveData { get; init; }

    /// <summary>Window size applied for rolling calculations.</summary>
    [Key(12)]
    public int WindowSize { get; init; }

    /// <summary>Optional VIX futures EOD data used as an external volatility input.</summary>
    [Key(13)]
    public ICollection<VixFuturesEodDataReadModel> VixEodData { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public InsertFuturesEodDataCommand() { }

    /// <summary>
    /// Creates a new command to insert a comprehensive futures EOD data payload.
    /// </summary>
    /// <param name="valueDate">Target value date.</param>
    /// <param name="futuresTickData">Latest futures tick data.</param>
    /// <param name="contract">Futures contract metadata.</param>
    /// <param name="eodDataToday">Today's EOD snapshot.</param>
    /// <param name="eodDataRange">Historical EOD data range.</param>
    /// <param name="normCurveData">Normal curve data used for enrichment.</param>
    /// <param name="windowSize">Rolling window size.</param>
    /// <param name="vixEodData">Optional VIX EOD data collection.</param>
    public InsertFuturesEodDataCommand(
        DateOnly valueDate,
        FuturesTickDataV2ReadModel futuresTickData,
        FuturesContractV2ReadModel contract,
        FuturesEodDataV2ReadModel eodDataToday,
        ICollection<FuturesEodDataV2ReadModel> eodDataRange,
        NormalCurveTableReadModel normCurveData,
        int windowSize,
        ICollection<VixFuturesEodDataReadModel> vixEodData)
    {
        ValueDate = valueDate;
        FuturesTickData = futuresTickData ?? throw new ArgumentNullException(nameof(futuresTickData));
        Contract = contract ?? throw new ArgumentNullException(nameof(contract));
        EodDataToday = eodDataToday ?? throw new ArgumentNullException(nameof(eodDataToday));
        EodDataRange = eodDataRange ?? throw new ArgumentNullException(nameof(eodDataRange));
        NormCurveData = normCurveData ?? throw new ArgumentNullException(nameof(normCurveData));
        WindowSize = windowSize;
        VixEodData = vixEodData ?? throw new ArgumentNullException(nameof(vixEodData));

        EntityId = new FuturesEodDataId(Contract.ContractId ?? string.Empty, ValueDate);
        ErrorCode = 5002;
        RouteTo = BoundedContextName.FuturesEodDataBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match property indices)
    [SerializationConstructor]
    public InsertFuturesEodDataCommand(
        Guid commandId,                                   // Key(0)
        ActorSubject subject,                             // Key(1)
        bool postEvents,                                  // Key(2)
        FuturesEodDataId entityId,                        // Key(3)
        int errorCode,                                    // Key(4)
        BoundedContextName routeTo,                       // Key(5)
        DateOnly valueDate,                               // Key(6)
        FuturesTickDataV2ReadModel futuresTickData,       // Key(7)
        FuturesContractV2ReadModel contract,              // Key(8)
        FuturesEodDataV2ReadModel eodDataToday,           // Key(9)
        ICollection<FuturesEodDataV2ReadModel> eodDataRange, // Key(10)
        NormalCurveTableReadModel normCurveData,          // Key(11)
        int windowSize,                                   // Key(12)
        ICollection<VixFuturesEodDataReadModel> vixEodData // Key(13)
    )
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;

        ValueDate = valueDate;
        FuturesTickData = futuresTickData;
        Contract = contract;
        EodDataToday = eodDataToday;
        EodDataRange = eodDataRange;
        NormCurveData = normCurveData;
        WindowSize = windowSize;
        VixEodData = vixEodData;
    }
}