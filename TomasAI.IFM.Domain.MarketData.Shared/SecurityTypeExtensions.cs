namespace TomasAI.IFM.Domain.MarketData.Shared;

public static class SecurityTypeExtensions
{
    public static string ToStringFast(this SecurityType value) => value switch
    {
        SecurityType.FOP => nameof(SecurityType.FOP),
        SecurityType.FUT => nameof(SecurityType.FUT),
        _ => value.ToString()
    };
}
