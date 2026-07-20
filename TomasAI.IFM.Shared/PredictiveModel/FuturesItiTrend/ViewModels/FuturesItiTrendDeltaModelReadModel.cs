using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

/// <summary>
/// MessagePack-serializable model statistics and serialized model payload for ITI trend delta
/// for a futures symbol across a date range.
/// </summary>
/// <remarks>
/// Follows the pattern used by other view models to be compatible with MessagePack:
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers.
/// - A full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiTrendDeltaModelReadModel
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

    /// <summary>Mean absolute error of model predictions.</summary>
    [Key(12)]
    public double MeanAbsoluteError { get; init; }

    /// <summary>Mean squared error of model predictions.</summary>
    [Key(13)]
    public double MeanSquaredError { get; init; }

    /// <summary>Root mean squared error of model predictions.</summary>
    [Key(14)]
    public double RootMeanSquaredError { get; init; }

    /// <summary>Loss function value used during training/evaluation.</summary>
    [Key(15)]
    public double LossFunction { get; init; }

    /// <summary>Coefficient of determination (R²) for model fit.</summary>
    [Key(16)]
    public double RSquared { get; init; }

    /// <summary>Serialized model payload (binary).</summary>
    [Key(17)]
    public byte[] ModelData { get; init; } = Array.Empty<byte>();

    /// <summary>Parameterless constructor for serializers.</summary>
    public FuturesItiTrendDeltaModelReadModel() { }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> indices).
    /// </summary>
    [SerializationConstructor]
    public FuturesItiTrendDeltaModelReadModel(
        string symbol,                  // Key(0)
        DateOnly valueDate,             // Key(1)
        DateOnly startDate,             // Key(2)
        DateOnly endDate,               // Key(3)
        int count,                      // Key(4)
        double maximum,                 // Key(5)
        double mean,                    // Key(6)
        double median,                  // Key(7)
        double minimum,                 // Key(8)
        double skewness,                // Key(9)
        double stdDev,                  // Key(10)
        double variance,                // Key(11)
        double meanAbsoluteError,       // Key(12)
        double meanSquaredError,        // Key(13)
        double rootMeanSquaredError,    // Key(14)
        double lossFunction,            // Key(15)
        double rSquared,                // Key(16)
        byte[]? modelData = null        // Key(17)
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
        MeanAbsoluteError = meanAbsoluteError;
        MeanSquaredError = meanSquaredError;
        RootMeanSquaredError = rootMeanSquaredError;
        LossFunction = lossFunction;
        RSquared = rSquared;
        ModelData = modelData ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Returns a JSON representation for debugging.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}