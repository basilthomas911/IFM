using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
namespace TomasAI.IFM.Application.Api.Server.Core.MarketData.InstrumentDefinitions;
/// <summary>Requests reference maintenance from the independent backend.</summary>
internal static class InstrumentDefinitionMaintenance
{
    public static async Task RunAsync(IConfiguration configuration, CancellationToken cancellationToken)
    {
        await using var manager = new NatsConnectionManager();
        await using var connection = new MarketDataHostConnection(manager, configuration["MarketDataHost:NatsUrl"] ?? "nats://127.0.0.1:4222", configuration["MarketDataHost:Scope"] ?? "development");
        using var cache = new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache();
        var api = new MarketDataApiClient(connection, new MarketDataReadMirror(cache));
        var snapshot = await api.RefreshInstrumentDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Published instrument_definition snapshot {snapshot.Id}: {snapshot.RecordCount} exact records.");
    }
}
