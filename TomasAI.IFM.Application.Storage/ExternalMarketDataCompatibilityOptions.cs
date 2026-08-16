namespace TomasAI.IFM.Application.Storage;

/// <summary>
/// Bounded defaults used only by the legacy no-argument external market-data
/// query contracts. New callers should always provide explicit dates.
/// </summary>
public sealed class ExternalMarketDataCompatibilityOptions
{
    public int TreasuryLookbackDays { get; set; } = 14;

    public int EconomicCalendarLookbackDays { get; set; } = 7;

    public int EconomicCalendarForwardDays { get; set; } = 7;

    public IReadOnlySet<string>? EconomicCalendarCountryCodes { get; set; }

    public ExternalMarketDataCompatibilityOptions Validate()
    {
        if (TreasuryLookbackDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TreasuryLookbackDays));
        }

        if (EconomicCalendarLookbackDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(EconomicCalendarLookbackDays));
        }

        if (EconomicCalendarForwardDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(EconomicCalendarForwardDays));
        }

        return this;
    }
}
