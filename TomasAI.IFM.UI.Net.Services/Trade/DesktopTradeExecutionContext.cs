using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.UI.Net.Services.Trade;
/// <summary>Explicit broker route for desktop submissions; never defaults a production session to an emulator.</summary>
public sealed record DesktopTradeExecutionContext(string AccountAlias, BrokerEnvironment Environment)
{
    public static DesktopTradeExecutionContext Resolve(IConfiguration? configuration, string appEnvironment)
    {
        var development = appEnvironment == "Development";
        var alias = configuration?["TradeBroker:AccountAlias"] ?? (development ? configuration?["TradeBroker:Emulator:AccountAlias"] ?? "IFM-EMULATOR-PAPER" : null);
        var mode = configuration?["TradeBroker:Environment"] ?? (development ? "Emulator" : null);
        if (string.IsNullOrWhiteSpace(alias) || !Enum.TryParse<BrokerEnvironment>(mode, out var environment) || !Enum.IsDefined(environment) || environment == BrokerEnvironment.Unknown)
            throw new InvalidOperationException("Configure TradeBroker:AccountAlias and TradeBroker:Environment before submitting an order.");
        if (development && environment != BrokerEnvironment.Emulator) throw new InvalidOperationException("Development desktop orders require the emulator.");
        return new(alias, environment);
    }
}
