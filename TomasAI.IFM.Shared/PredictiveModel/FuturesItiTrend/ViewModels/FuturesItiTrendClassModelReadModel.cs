using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels
{
    /// <summary>
    /// MessagePack-serializable model statistics and serialized model payload for ITI trend class
    /// for a futures symbol across a date range.
    /// </summary>
    /// <remarks>
    /// Follows the project's MessagePack pattern:
    /// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
    /// - A parameterless constructor for serializers.
    /// - A full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
    /// </remarks>
    [MessagePackObject(AllowPrivate = true)]
    public record FuturesItiTrendClassModelReadModel
    {
        /// <summary>Underlying futures symbol.</summary>
        [Key(0)]
        public string Symbol { get; init; } = string.Empty;

        /// <summary>As-of (value) date for the snapshot.</summary>
        [Key(1)]
        public DateOnly ValueDate { get; init; }

        /// <summary>Model training start date.</summary>
        [Key(2)]
        public DateOnly StartDate { get; init; }

        /// <summary>Model training end date.</summary>
        [Key(3)]
        public DateOnly EndDate { get; init; }

        /// <summary>Number of observations used to build the model.</summary>
        [Key(4)]
        public int Count { get; init; }

        /// <summary>Maximum observed value.</summary>
        [Key(5)]
        public double Maximum { get; init; }

        /// <summary>Mean of observed values.</summary>
        [Key(6)]
        public double Mean { get; init; }

        /// <summary>Median of observed values.</summary>
        [Key(7)]
        public double Median { get; init; }

        /// <summary>Minimum observed value.</summary>
        [Key(8)]
        public double Minimum { get; init; }

        /// <summary>Skewness of the distribution.</summary>
        [Key(9)]
        public double Skewness { get; init; }

        /// <summary>Standard deviation.</summary>
        [Key(10)]
        public double StdDev { get; init; }

        /// <summary>Variance.</summary>
        [Key(11)]
        public double Variance { get; init; }

        /// <summary>Classification accuracy.</summary>
        [Key(12)]
        public double Accuracy { get; init; }

        /// <summary>Area under the precision-recall curve.</summary>
        [Key(13)]
        public double AreaUnderPrecisionRecallCurve { get; init; }

        /// <summary>Area under the ROC curve.</summary>
        [Key(14)]
        public double AreaUnderRocCurve { get; init; }

        /// <summary>Entropy metric for the model.</summary>
        [Key(15)]
        public double Entropy { get; init; }

        /// <summary>F1 score for the classifier.</summary>
        [Key(16)]
        public double F1Score { get; init; }

        /// <summary>Serialized model payload (binary).</summary>
        [Key(17)]
        public byte[] ModelData { get; init; } = Array.Empty<byte>();

        /// <summary>Parameterless constructor for serializers.</summary>
        public FuturesItiTrendClassModelReadModel() { }

        /// <summary>
        /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> indices).
        /// </summary>
        [SerializationConstructor]
        public FuturesItiTrendClassModelReadModel(
            string symbol,                             // Key(0)
            DateOnly valueDate,                        // Key(1)
            DateOnly startDate,                        // Key(2)
            DateOnly endDate,                          // Key(3)
            int count,                                 // Key(4)
            double maximum,                            // Key(5)
            double mean,                               // Key(6)
            double median,                             // Key(7)
            double minimum,                            // Key(8)
            double skewness,                           // Key(9)
            double stdDev,                             // Key(10)
            double variance,                           // Key(11)
            double accuracy,                           // Key(12)
            double areaUnderPrecisionRecallCurve,      // Key(13)
            double areaUnderRocCurve,                  // Key(14)
            double entropy,                            // Key(15)
            double f1Score,                            // Key(16)
            byte[]? modelData = null                   // Key(17)
        )
        {
            Symbol = symbol ?? string.Empty;
            ValueDate = valueDate;
            StartDate = startDate;
            EndDate = endDate;
            Count = count;
            Maximum = maximum;
            Mean = mean;
            Median = median;
            Minimum = minimum;
            Skewness = skewness;
            StdDev = stdDev;
            Variance = variance;
            Accuracy = accuracy;
            AreaUnderPrecisionRecallCurve = areaUnderPrecisionRecallCurve;
            AreaUnderRocCurve = areaUnderRocCurve;
            Entropy = entropy;
            F1Score = f1Score;
            ModelData = modelData ?? Array.Empty<byte>();
        }

        /// <summary>Returns a JSON representation for debugging.</summary>
        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}