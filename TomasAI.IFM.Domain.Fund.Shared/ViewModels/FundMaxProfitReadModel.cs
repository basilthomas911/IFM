using Newtonsoft.Json;
using MessagePack;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// Represents the maximum profit and associated risk percentage for a specific fund order.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class FundMaxProfitReadModel
{
    /// <summary>
    /// The unique identifier of the fund order to which the profit and risk data apply.
    /// </summary>
    [Key(0)] public FundOrderId FundOrderId { get; set; }

    /// <summary>
    /// The maximum profit value calculated for the specified fund order.
    /// </summary>
    [Key(1)] public decimal FundMaxProfit { get; set; }

    /// <summary>
    /// The percentage value representing the risk associated with achieving the maximum profit for the fund order.
    /// </summary>
    [Key(2)] public double FundRiskPercent { get; set; }

    /// <summary>
    /// Default constructor for serializers that require a parameterless constructor.
    /// </summary>
    public FundMaxProfitReadModel()
    {
    }

    /// <summary>
    /// MessagePack constructor used for deserialization.
    /// </summary>
    [SerializationConstructor]
    public FundMaxProfitReadModel(FundOrderId fundOrderId, decimal fundMaxProfit, double fundRiskPercent)
    {
        FundOrderId = fundOrderId;
        FundMaxProfit = fundMaxProfit;
        FundRiskPercent = fundRiskPercent;
    }

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => FundOrderId.FundId > 0 && FundOrderId.OrderId > 0;
}
