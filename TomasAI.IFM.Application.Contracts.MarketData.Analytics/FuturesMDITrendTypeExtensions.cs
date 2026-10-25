namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve this existing persisted CLR type identity and acronym spelling.")]
public static class FuturesMDITrendTypeExtensions
{
    public static string ToStringFast(this FuturesMDITrendType value) => value switch
    {
        FuturesMDITrendType.RangeBound => nameof(FuturesMDITrendType.RangeBound),
        FuturesMDITrendType.UpTrending => nameof(FuturesMDITrendType.UpTrending),
        FuturesMDITrendType.DownTrending => nameof(FuturesMDITrendType.DownTrending),
        _ => value.ToString()
    };
}
