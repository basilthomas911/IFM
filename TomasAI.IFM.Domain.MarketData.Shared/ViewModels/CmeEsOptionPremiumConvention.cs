namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Reviewed premium-settlement terms for CME E-mini S&amp;P 500 options, independent of exercise style.</summary>
public static class CmeEsOptionPremiumConvention
{
    public const string Version = "CME-ES-PremiumPaid/v1";
    public const string Evidence = "https://www.cmegroup.com/articles/2024/index-options-box-spreads-as-financing-tool.html";
    public const ReferencePremiumStyle PremiumStyle = ReferencePremiumStyle.PremiumPaid;

    /// <summary>Identifies the reviewed ES product family without guessing conventions for other roots or exchanges.</summary>
    public static bool AppliesTo(string rootSymbol, string exchange)
        => string.Equals(rootSymbol, "ES", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(exchange, "XCME", StringComparison.OrdinalIgnoreCase)
                || string.Equals(exchange, "CME", StringComparison.OrdinalIgnoreCase));

    /// <summary>Rejects conflicting ES premium terms; other products still require their own explicit reviewed terms.</summary>
    public static bool IsConsistent(string rootSymbol, string exchange, ReferencePremiumStyle premiumStyle)
        => !AppliesTo(rootSymbol, exchange) || premiumStyle == PremiumStyle;
}
