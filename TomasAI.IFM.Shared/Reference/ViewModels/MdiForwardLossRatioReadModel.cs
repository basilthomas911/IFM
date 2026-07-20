using System;
using MessagePack;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Reference.ViewModels
{
    /// <summary>
    /// MessagePack-serializable view model mapping an MDI bucket, trend direction, and trade type
    /// to a forward loss ratio with audit metadata.
    /// </summary>
    /// <remarks>
    /// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys;
    /// derived members are excluded from MessagePack via IgnoreMember. Includes parameterless and value constructors.
    /// </remarks>
    [MessagePackObject(AllowPrivate = true)]
    public record MDIForwardLossRatioReadModel
    {
        /// <summary>Market Direction Indicator (MDI) bucket.</summary>
        [Key(0)]
        public int MDI { get; init; }

        /// <summary>Trend direction associated with the MDI bucket.</summary>
        [Key(1)]
        public IntrinsicTimeTrendType TrendDirection { get; init; }

        /// <summary>Trade strategy/type this ratio applies to.</summary>
        [Key(2)]
        public TradeType TradeType { get; init; }

        /// <summary>Estimated forward loss ratio.</summary>
        [Key(3)]
        public double ForwardLossRatio { get; init; }

        /// <summary>User or system that created the record.</summary>
        [Key(4)]
        public string CreatedBy { get; init; } = string.Empty;

        /// <summary>Creation timestamp (nullable).</summary>
        [Key(5)]
        public DateTime? CreatedOn { get; init; }

        /// <summary>User or system that last updated the record.</summary>
        [Key(6)]
        public string UpdatedBy { get; init; } = string.Empty;

        /// <summary>Last update timestamp (nullable).</summary>
        [Key(7)]
        public DateTime? UpdatedOn { get; init; }

        /// <summary>Parameterless constructor for serializers.</summary>
        public MDIForwardLossRatioReadModel() { }

        /// <summary>
        /// Creates a new MDI forward loss ratio view model.
        /// </summary>
        /// <param name="mdi">MDI bucket value.</param>
        /// <param name="trendDirection">Trend direction.</param>
        /// <param name="tradeType">Trade type.</param>
        /// <param name="forwardLossRatio">Forward loss ratio.</param>
        /// <param name="createdBy">Creator identity.</param>
        /// <param name="createdOn">Creation timestamp.</param>
        /// <param name="updatedBy">Last updater identity.</param>
        /// <param name="updatedOn">Last update timestamp.</param>
        public MDIForwardLossRatioReadModel(
            int mdi,
            IntrinsicTimeTrendType trendDirection,
            TradeType tradeType,
            double forwardLossRatio,
            string createdBy,
            DateTime? createdOn,
            string updatedBy,
            DateTime? updatedOn)
        {
            MDI = mdi;
            TrendDirection = trendDirection;
            TradeType = tradeType;
            ForwardLossRatio = forwardLossRatio;
            CreatedBy = createdBy ?? string.Empty;
            CreatedOn = createdOn;
            UpdatedBy = updatedBy ?? string.Empty;
            UpdatedOn = updatedOn;
        }

        /// <summary>
        /// Derived identifier for this configuration (excluded from MessagePack).
        /// </summary>
        [IgnoreMember]
        public MDIForwardLossRatioId Id => MDIForwardLossRatioId.Create(MDI, TrendDirection, TradeType);
    }
}