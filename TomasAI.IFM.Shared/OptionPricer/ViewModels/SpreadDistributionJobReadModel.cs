using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.ViewModels;

/// <summary>
/// MessagePack-serializable view model describing a spread distribution job for an option trade.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys,
/// and derived/utility members excluded via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record SpreadDistributionJobReadModel
{
    /// <summary>Order identifier associated with the job.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier associated with the job.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>The type of option trade.</summary>
    [Key(2)]
    public TradeType TradeType { get; init; }

    /// <summary>The status of the trade when the job was created.</summary>
    [Key(3)]
    public TradeStatus TradeStatus { get; init; }

    /// <summary>The value (trading) date associated with this job.</summary>
    [Key(4)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Remaining days to expiry for the underlying position.</summary>
    [Key(5)]
    public int DaysToExpiry { get; init; }

    /// <summary>UTC timestamp when the job was submitted.</summary>
    [Key(6)]
    public DateTime JobSubmitted { get; init; }

    /// <summary>Current job status (textual).</summary>
    [Key(7)]
    public SpreadDistributionJobStatus JobStatus { get; init; }

    /// <summary>UTC timestamp when the job was completed, if applicable.</summary>
    [Key(8)]
    public DateTime? JobCompleted { get; init; }

    /// <summary>UTC timestamp when the job failed, if applicable.</summary>
    [Key(9)]
    public DateTime? JobFailed { get; init; }

    /// <summary>True if the job is currently in progress.</summary>
    [Key(10)]
    public bool InProgress { get; init; }

    /// <summary>Loss probability factor used for prioritization/scheduling.</summary>
    [Key(11)]
    public double LossProbabilityFactor { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers and tooling.
    /// </summary>
    public SpreadDistributionJobReadModel() { }

    /// <summary>
    /// Creates a new spread distribution job view model.
    /// </summary>
    public SpreadDistributionJobReadModel(
        int orderId,
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry,
        DateTime jobSubmitted,
        SpreadDistributionJobStatus jobStatus,
        DateTime? jobCompleted,
        DateTime? jobFailed,
        bool inProgress,
        double lossProbabilityFactor)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        TradeStatus = tradeStatus;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        JobSubmitted = jobSubmitted;
        JobStatus = jobStatus;
        JobCompleted = jobCompleted;
        JobFailed = jobFailed;
        InProgress = inProgress;
        LossProbabilityFactor = lossProbabilityFactor;
    }

    /// <summary>Composite job entity identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public SpreadDistributionJobEntityId EntityId => new(OrderId, TradeId, ValueDate);

    /// <summary>Put-side distribution payload attached post-processing (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public SpreadDistributionReadModel? PutSpreadDistribution { get; set; }

    /// <summary>Call-side distribution payload attached post-processing (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public SpreadDistributionReadModel? CallSpreadDistribution { get; set; }

    /// <summary>Processing duration in seconds or milliseconds (client-defined; excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public double Duration { get; set; }

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid
        => OrderId > 0 && TradeId > 0 && ValueDate > DateOnly.MinValue;
}