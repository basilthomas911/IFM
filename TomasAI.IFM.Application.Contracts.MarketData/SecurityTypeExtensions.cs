namespace TomasAI.IFM.Domain.MarketData.Shared;

public static class SecurityTypeExtensions
{

    extension(SecurityType value)
    {
        public string ToStringFast() => value switch
        {
            SecurityType.FOP => nameof(SecurityType.FOP),
            SecurityType.FUT => nameof(SecurityType.FUT),
            _ => value.ToString()
        };
    }
}
