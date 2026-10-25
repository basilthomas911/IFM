namespace TomasAI.IFM.Domain.MarketData.Shared;

public static class EconomicCalendarQueryLimits
{
    public const int MaximumRangeMonths = 120;
    public const int MaximumCountryCodes = 32;
    public const int MaximumPartitions = 512;
    public const int MaximumPageSize = 500;
    public const int MaximumRowsPerPartition = 2_500;
    public const int MaximumContinuationTokenLength = 2_048;
}
