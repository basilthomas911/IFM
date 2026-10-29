using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
namespace TomasAI.IFM.Application.MarketData.Rust.Host.Seeding;
/// <summary>Owns cold replay endpoint configuration without generated credential
/// formatting. Read-only replay owns an independent Tokio socket and reader slot.</summary>
public sealed class NativeSeedReplaySource
{
    readonly NativeMarketDataRuntime runtime;readonly string key;readonly string[] dns;
    readonly IReadOnlyDictionary<string,NativeDatasetEndpoint> endpoints;
    /// <summary>Receives configured dataset endpoints, never inferring a dataset
    /// or gateway from the canonical domain contract's spelling.</summary>
    public NativeSeedReplaySource(NativeMarketDataRuntime runtime,string apiKey,string[] numericDnsServers,IReadOnlyDictionary<string,NativeDatasetEndpoint> endpoints)
    {this.runtime=runtime;key=apiKey;dns=(string[])numericDnsServers.Clone();this.endpoints=new Dictionary<string,NativeDatasetEndpoint>(endpoints,StringComparer.Ordinal);}
    /// <summary>Opens the explicitly resolved contract under the original budget.</summary>
    public IHistoricalRecordReader Open(DatabentoContractRegistration registration,DateTimeOffset start,TimeSpan budget,CancellationToken token)
    {
        var dataset=registration.Dataset??throw new InvalidOperationException("Indicator seed contract has no resolved dataset.");
        if(!endpoints.TryGetValue(dataset,out var endpoint))throw new InvalidOperationException("Indicator seed dataset endpoint is not configured.");
        return runtime.OpenFuturesReplay(dataset,registration.ProviderContractName,key,endpoint.Host,endpoint.Port,dns,start,4096,budget,token);
    }
}
