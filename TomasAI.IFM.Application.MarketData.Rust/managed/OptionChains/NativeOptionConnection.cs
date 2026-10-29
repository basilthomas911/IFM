using System.Globalization;
using System.Net;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Rust.OptionChains;

/// <summary>Explicit cancellable provider endpoint for native option scopes.
/// Credentials have no generated record formatting and never enter diagnostics.
/// The immutable DNS list uses numeric socket endpoints; no OS resolver is used.</summary>
public sealed class NativeOptionConnection
{
    readonly string key, host;
    readonly ushort port;
    readonly string[] dns;
    /// <summary>Validates the copied cold endpoint configuration without opening
    /// a connection. DNS entries must include their port, normally 53.</summary>
    public NativeOptionConnection(string apiKey,string host,ushort port,string[] numericDnsServers)
    {
        ArgumentNullException.ThrowIfNull(apiKey);ArgumentNullException.ThrowIfNull(numericDnsServers);ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if(apiKey.Length!=32||!apiKey.All(char.IsAscii)||port==0||host.Length>253||host.Any(char.IsWhiteSpace)||host.Any(char.IsControl)
            ||numericDnsServers.Length is <1 or >4||numericDnsServers.Any(x=>!IPEndPoint.TryParse(x,out var endpoint)||endpoint.Port==0))throw new ArgumentException("Invalid native provider endpoint configuration.");
        key=apiKey;this.host=host;this.port=port;dns=(string[])numericDnsServers.Clone();
    }
    internal Task StartUnderlyingAsync(NativeMarketDataRuntime runtime,string scope,Guid generation,NativeRegistration underlying,TimeSpan remaining,CancellationToken token)
        =>runtime.StartOptionScopeAsync(scope,PricingSemanticHash.Compute(underlying),generation,key,host,port,dns,
            [new([underlying.Source],"mbp-1","instrument_id")],[underlying with{Required=true,Vwap=false}],true,remaining,token);
    internal Task StartAsync(NativeMarketDataRuntime runtime,string scope,string physicalDigest,Guid generation,
        NativeRegistration underlying,PreparedOptionPricingContext[] options,OptionChainStatisticsSnapshot[] statistics,
        string pricingDigest,OptionPricingRefreshPolicy policy,bool production,DateTimeOffset at,long? replayStartNanoseconds,
        TimeSpan remaining,CancellationToken token)
    {
        var registrations=options.Select(x=>new NativeRegistration(x.Context.Contract.InstrumentId,x.Context.Contract.ContractId,
            x.Context.Contract.InstrumentId.ToString(CultureInfo.InvariantCulture),true,null,false,x.RiskRequired,x.Context.Contract.PublisherId)).Prepend(underlying with{Source=underlying.Instrument.ToString(CultureInfo.InvariantCulture),Required=true}).ToArray();
        var all=registrations.Select(x=>x.Source).ToArray();var optionIds=registrations.Where(x=>x.Option).Select(x=>x.Source).ToArray();
        NativeSubscription[] subscriptions=[new(all,"trades","instrument_id",replayStartNanoseconds),new(all,"mbp-1","instrument_id"),new(optionIds,"statistics","instrument_id")];
        return runtime.StartPricedOptionScopeAsync(scope,physicalDigest,generation,key,host,port,dns,subscriptions,registrations,true,
            pricingDigest,options,policy,production,at,remaining,statistics,token);
    }
}

