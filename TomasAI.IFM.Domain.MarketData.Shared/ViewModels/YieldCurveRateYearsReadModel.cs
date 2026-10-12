using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable view model containing an array of years that have yield curve rate data.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record YieldCurveRateYearsReadModel
{
    /// <summary>Array of years that have yield curve rate data.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
    [Key(0)]
    public int[] Years { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public YieldCurveRateYearsReadModel()
    {
        Years = [];
    }

    /// <summary>
    /// Constructor to create a years view model with the specified years.
    /// </summary>
    public YieldCurveRateYearsReadModel(int[] years)
    {
        Years = years;
    }

    [IgnoreMember]
    public bool IsValid => Years != null && Years.Length > 0;
}
