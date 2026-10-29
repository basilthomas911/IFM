using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.ReferenceData;

namespace TomasAI.IFM.Application.MarketData.Rust.Host;

/// <summary>Host lifetime for the complete native dataset owner. The separate
/// cold-query runtime keeps historical imports and catalog queries outside the
/// three live dataset slots. No managed epoch, lease, gate or renewal loop is
/// retained here. Each live dataset owns its own native recovery episode.</summary>
public sealed class NativeDatasetBackend : IAsyncDisposable
{
    readonly IDatabentoContractRegistrationRegistry registrations;
    readonly DatabentoMarketDataRuntimeOptions options;
    readonly IConfiguration configuration;
    readonly NativeOwnedSession session;
    readonly NativeColdBackend cold;
    readonly Lazy<bool> history;
    readonly object disposalGate=new();
    Task? disposal;
    /// <summary>Command/query handle; it exposes no physical provider resource.</summary>
    public NativeMarketDataRuntime Runtime { get; }
    /// <summary>Copied provider configuration for the independent options lane.</summary>
    public NativeOwnerGateway OptionGateway => Gateway(cold,MarketDataLane.OptionsGlbx);
    /// <summary>Copied broker configuration. Rust owns publication and retry.</summary>
    public NativeOwnerBroker Broker { get; }
    /// <summary>Explicit deployment subject scope, shared with the current API.</summary>
    public string Scope { get; }
    /// <summary>Unprepared global reference policy; Rust reconstructs contexts.</summary>
    public NativeOwnerReferencePolicy Policy { get; }

    /// <summary>Creates a native root without opening a provider connection.
    /// Configuration does not select this backend or alter the existing backend.</summary>
    public NativeDatasetBackend(NativeColdBackend cold, IConfiguration configuration,
        TimeProvider clock, IDatabentoContractRegistrationRegistry registrations,
        DatabentoMarketDataRuntimeOptions options)
    {
        this.configuration=configuration;this.registrations=registrations;this.options=options;this.cold=cold;
        Scope=configuration["MarketDataHost:Scope"]??"development";
        Broker=new(configuration["MarketDataHost:NatsUrl"]??"nats://127.0.0.1:4222",cold.Dns);
        Policy=new(UsTreasuryPublicationCalendar.Default2026,UsTreasuryCurve.ConversionPolicy,
            options.OptionPricingRefresh,options.FeedOptions.DeploymentProfile==FeedDeploymentProfile.Production,
            options.FeedOptions.DeploymentProfile==FeedDeploymentProfile.Development
                &&configuration.GetValue<bool>("MarketDataRecovery:Stage3:Enabled")
                &&configuration.GetValue<bool>("MarketDataRecovery:Stage3:AllowDevelopmentLiveQualification")?2000:0);
        Runtime=new(cold.LibraryPath,configuration.GetValue("MarketDataHost:Rust:Threads",2u));
        try
        {
            if(!Runtime.SupportsDatasetOwnerComplete)throw new InvalidOperationException("The native library lacks complete dataset-owner capabilities.");
            Runtime.ConfigureApiClock(clock.GetUtcNow(),configuration.GetValue("MarketDataHost:Rust:AdvancingClock",true));
            history=new(()=>{Runtime.ConfigureHistory(cold.HistoryEndpoint,cold.ProviderKey,cold.Dns);return true;},LazyThreadSafetyMode.ExecutionAndPublication);
            Runtime.ConfigureTreasury(configuration["MarketDataHost:Rust:TreasuryEndpoint"]??UsTreasuryCurve.Endpoint,cold.Dns);
            Runtime.CreateDatasetOwner();
            session=new(Runtime,Broker,Scope);
        }
        catch
        {
            // No commands were accepted yet. Always retire the native runtime,
            // including its completion pump, if configuration is rejected.
            Runtime.StopAndDisposeAsync(TimeSpan.FromMinutes(2)).AsTask().GetAwaiter().GetResult();throw;
        }
    }
    NativeOwnerGateway Gateway(NativeColdBackend cold,MarketDataLane lane)
    {
        var xcbf=lane==MarketDataLane.FuturesXcbf;
        return new(cold.ProviderKey,configuration[xcbf?"MarketDataHost:Rust:XcbfHost":"MarketDataHost:Rust:GlbxHost"]
            ??(xcbf?"xcbf-pitch.lsg.databento.com":"glbx-mdp3.lsg.databento.com"),
            configuration.GetValue<ushort>(xcbf?"MarketDataHost:Rust:XcbfPort":"MarketDataHost:Rust:GlbxPort",13000),cold.Dns);
    }
    /// <summary>Copies configured root demand. Provider resolution, source plans,
    /// setup, publication and subsequent replacements execute inside Rust.</summary>
    public NativeOwnedFuturesDemand[] Demands(NativeColdBackend cold)=>registrations.Snapshot()
        .Where(row=>row.AssetTypeId==TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures)
        .GroupBy(row=>row.Dataset??options.FeedOptions.Dataset,StringComparer.Ordinal)
        .Select(dataset=>
        {
            var lane=dataset.Key switch{"GLBX.MDP3"=>MarketDataLane.FuturesGlbx,"XCBF.PITCH"=>MarketDataLane.FuturesXcbf,
                _=>throw new InvalidOperationException("Unsupported native futures dataset: "+dataset.Key)};
            var roots=dataset.GroupBy(row=>row.RootSymbol,StringComparer.Ordinal).Select(root=>
            {
                if(string.IsNullOrWhiteSpace(root.Key))throw new InvalidDataException("A native configured future requires its explicit root symbol.");
                return new NativeOwnerFuturesRoot(root.Key,checked((uint)root.Count()),
                    options.FuturesContractCurrencyFallbacks.GetValueOrDefault(root.Key),root.Key=="ES");
            }).ToArray();
            return new NativeOwnedFuturesDemand(lane,Gateway(cold,lane),roots);
        }).ToArray();
    /// <summary>Accepts background and futures intent without managed preparation.
    /// Only the native futures readiness observer gates session start; optional
    /// background discovery owns its independent native deadline and error path.</summary>
    public async Task StartAsync(DateOnly date,NativeOwnedFuturesDemand[] demands,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _=history.Value;
        Runtime.ConfigureDatasetOwnerBackground(date,date,Scope,OptionGateway,Broker,Policy,
            new(configuration["MarketDataHost:DeploymentProfile"]??options.FeedOptions.DeploymentProfile.ToString(),Committed:true));
        await session.StartAsync(date,date,demands,token).ConfigureAwait(false);
    }
    /// <summary>Native cancellation and join, independent of every C# consumer.</summary>
    public Task StopAsync(CancellationToken token=default)
    {
        // DI may dispose the API alias after the Host lifetime has already joined
        // the root. Observe that same join instead of calling a freed FFI handle.
        lock(disposalGate)return disposal is{} retired?retired.WaitAsync(token):session.StopAsync(token);
    }
    /// <summary>Joins the native root once. Repeated Host/DI disposal observes the
    /// same result and cannot submit commands to an already freed runtime.</summary>
    public ValueTask StopAndDisposeAsync(TimeSpan budget)
    {lock(disposalGate)return new(disposal??=Runtime.StopAndDisposeAsync(budget).AsTask());}
    /// <inheritdoc/>
    public ValueTask DisposeAsync()=>StopAndDisposeAsync(TimeSpan.FromMinutes(2));
}
