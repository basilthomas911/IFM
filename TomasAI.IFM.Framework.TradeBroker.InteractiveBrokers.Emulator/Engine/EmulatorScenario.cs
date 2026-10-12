using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>Conservative synthetic execution settings; no broker-equivalent margin is implied.</summary>
public sealed record EmulatorScenario(
    string AccountAlias,
    string Currency,
    decimal StartingCash,
    decimal PerLegCommission,
    TimeSpan MaximumQuoteAge,
    int MaximumStrategyUnitsPerFill = int.MaxValue,
    EmulatorFaultProfile? Faults = null)
{
    public static EmulatorScenario Development(string accountAlias) => new(accountAlias, "USD", 1_000_000m, 0.65m, TimeSpan.FromSeconds(2));
}
