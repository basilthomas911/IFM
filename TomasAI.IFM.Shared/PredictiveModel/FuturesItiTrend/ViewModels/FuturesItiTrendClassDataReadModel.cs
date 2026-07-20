using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels
{
    /// <summary>
    /// MessagePack-serializable view model containing ITI trend class data for a futures symbol on a specific value date.
    /// </summary>
    /// <remarks>
    /// Pattern mirrors <see cref="TomasAI.IFM.Shared.MarketDataFeed.ViewModels.FuturesEodClosingPriceReadModel"/>:
    /// - Explicit properties with sequential MessagePack keys.
    /// - A parameterless constructor for serializers.
    /// - A full constructor annotated with <see cref="SerializationConstructorAttribute"/> for MessagePack deserialization.
    /// </remarks>
    [MessagePackObject(AllowPrivate = true)]
    public record FuturesItiTrendClassDataReadModel
    {
        /// <summary>Underlying futures symbol.</summary>
        [Key(0)]
        public string Symbol { get; init; }

        /// <summary>As-of (value) date for this snapshot.</summary>
        [Key(1)]
        public DateOnly ValueDate { get; init; }

        /// <summary>Timestamp when the measurement was taken.</summary>
        [Key(2)]
        public DateTime Timestamp { get; init; }

        /// <summary>Sequence identifier for ordering messages.</summary>
        [Key(3)]
        public long SequenceId { get; init; }

        /// <summary>Trend class metric.</summary>
        [Key(4)]
        public float TrendClass { get; init; }

        /// <summary>Trend direction metric.</summary>
        [Key(5)]
        public float TrendDirection { get; init; }

        /// <summary>Trend direction mode indicator.</summary>
        [Key(6)]
        public float TrendDirectionMode { get; init; }

        /// <summary>Calculated ITI trend delta.</summary>
        [Key(7)]
        public float TrendDelta { get; init; }

        /// <summary>Futures Relative Strength Index (RSI) value.</summary>
        [Key(8)]
        public float FuturesRSI { get; init; }

        /// <summary>Parameterless constructor for serializers.</summary>
        public FuturesItiTrendClassDataReadModel() { }

        /// <summary>
        /// MessagePack serialization constructor (keys must match <see cref="KeyAttribute"/> indices).
        /// </summary>
        [SerializationConstructor]
        public FuturesItiTrendClassDataReadModel(
            string symbol,            // Key(0)
            DateOnly valueDate,       // Key(1)
            DateTime timestamp,       // Key(2)
            long sequenceId,          // Key(3)
            float trendClass,         // Key(4)
            float trendDirection,     // Key(5)
            float trendDirectionMode, // Key(6)
            float trendDelta,         // Key(7)
            float futuresRsi)         // Key(8)
        {
            Symbol = symbol ?? string.Empty;
            ValueDate = valueDate;
            Timestamp = timestamp;
            SequenceId = sequenceId;
            TrendClass = trendClass;
            TrendDirection = trendDirection;
            TrendDirectionMode = trendDirectionMode;
            TrendDelta = trendDelta;
            FuturesRSI = futuresRsi;
        }

        /// <summary>Returns a JSON representation for debugging.</summary>
        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}