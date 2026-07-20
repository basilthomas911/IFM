using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels
{
    /// <summary>
    /// MessagePack-serializable view model containing ITI trend delta data for a futures symbol on a specific value date.
    /// </summary>
    /// <remarks>
    /// Pattern mirrors <see cref="TomasAI.IFM.Shared.MarketDataFeed.ViewModels.FuturesEodClosingPriceReadModel"/>:
    /// - Explicit properties with sequential MessagePack keys.
    /// - Parameterless constructor for serializers.
    /// - A full constructor annotated with <see cref="SerializationConstructorAttribute"/> for MessagePack deserialization.
    /// </remarks>
    [MessagePackObject(AllowPrivate = true)]
    public record FuturesItiTrendDeltaDataReadModel
    {
        /// <summary>Underlying futures symbol.</summary>
        [Key(0)]
        public string Symbol { get; init; } = string.Empty;

        /// <summary>As-of (value) date for this snapshot.</summary>
        [Key(1)]
        public DateOnly ValueDate { get; init; }

        /// <summary>Timestamp when the measurement was taken.</summary>
        [Key(2)]
        public DateTime Timestamp { get; init; }

        /// <summary>Sequence identifier for ordering messages.</summary>
        [Key(3)]
        public long SequenceId { get; init; }

        /// <summary>Calculated ITI trend delta.</summary>
        [Key(4)]
        public float TrendDelta { get; init; }

        /// <summary>Trend direction metric.</summary>
        [Key(5)]
        public float TrendDirection { get; init; }

        /// <summary>Trend direction mode indicator.</summary>
        [Key(6)]
        public float TrendDirectionMode { get; init; }

        /// <summary>Futures price at the time of the snapshot.</summary>
        [Key(7)]
        public float FuturesPrice { get; init; }

        /// <summary>Observed trend extreme value.</summary>
        [Key(8)]
        public float TrendExtreme { get; init; }

        /// <summary>Futures Relative Strength Index (RSI) value.</summary>
        [Key(9)]
        public float FuturesRSI { get; init; }

        /// <summary>Parameterless constructor for serializers.</summary>
        public FuturesItiTrendDeltaDataReadModel() { }

        /// <summary>
        /// MessagePack serialization constructor (keys must match <see cref="KeyAttribute"/> indices).
        /// </summary>
        [SerializationConstructor]
        public FuturesItiTrendDeltaDataReadModel(
            string symbol,           // Key(0)
            DateOnly valueDate,      // Key(1)
            DateTime timestamp,      // Key(2)
            long sequenceId,         // Key(3)
            float trendDelta,        // Key(4)
            float trendDirection,    // Key(5)
            float trendDirectionMode,// Key(6)
            float futuresPrice,      // Key(7)
            float trendExtreme,      // Key(8)
            float futuresRsi)        // Key(9)
        {
            Symbol = symbol ?? string.Empty;
            ValueDate = valueDate;
            Timestamp = timestamp;
            SequenceId = sequenceId;
            TrendDelta = trendDelta;
            TrendDirection = trendDirection;
            TrendDirectionMode = trendDirectionMode;
            FuturesPrice = futuresPrice;
            TrendExtreme = trendExtreme;
            FuturesRSI = futuresRsi;
        }

        /// <summary>Returns a JSON representation for debugging.</summary>
        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}