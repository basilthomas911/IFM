namespace TomasAI.IFM.Application.Storage;

/// <summary>
/// Bounded defaults used only by the legacy no-argument external market-data
/// query contracts. New callers should always provide explicit dates.
/// </summary>
public sealed class ExternalMarketDataCompatibilityOptions
{
    public int TreasuryLookbackDays { get; set; } = 14;

    public ExternalMarketDataCompatibilityOptions Validate()
    {
        if (TreasuryLookbackDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TreasuryLookbackDays));
        }

        return this;
    }
}
