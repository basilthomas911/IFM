namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public sealed class FmpMarketDataImportOptions
{
    public int MaximumRangeDays { get; set; } = 366;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "Instance validation reports the configuration property captured by this object.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "Instance validation reports the configuration property captured by this object.")]
    internal void Validate()
    {
        if (MaximumRangeDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumRangeDays));
        }
    }
}
