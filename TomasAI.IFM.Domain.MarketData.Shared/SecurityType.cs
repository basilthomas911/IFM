namespace TomasAI.IFM.Domain.MarketData.Shared;

public enum SecurityType
{
    FOP,
    FUT
}

public static class SecurityTypeExtensions
{
    public static string ToStringFast(this SecurityType value) => value switch
    {
        SecurityType.FOP => nameof(SecurityType.FOP),
        SecurityType.FUT => nameof(SecurityType.FUT),
        _ => value.ToString()
    };
}
