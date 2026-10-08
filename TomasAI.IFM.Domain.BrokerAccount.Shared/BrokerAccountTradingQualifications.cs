using TomasAI.IFM.Application.TradeBroker.Contracts;
namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Derives trading eligibility from persisted account data and the trusted host environment.</summary>
public static class BrokerAccountTradingQualifications
{
    /// <summary>Annotates a persisted account with the Development emulator exemption.</summary>
    /// <param name="account">The stored account definition.</param>
    /// <param name="isDevelopment">Whether the trusted API host is Development.</param>
    /// <returns>A temporary query view; qualification evidence and holds are unchanged.</returns>
    public static BrokerAccountDefinition TradingView(BrokerAccountDefinition account, bool isDevelopment)
        => account with { DevelopmentQualificationsExempt = isDevelopment && account.Environment == BrokerEnvironment.Emulator };
}
