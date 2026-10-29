using System.Text.Json;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>Copied provider command arguments. No field is a native resource lease.</summary>
public sealed record NativeOwnerSource(string Key, string Host, ushort Port, string[] DnsServers,
    NativeSubscription[] Subscriptions, NativeRegistration[] Registrations, bool Observations = true)
{
    /// <summary>Credentials must never appear in incidental configuration logs.</summary>
    public override string ToString() => "Native provider configuration (credentials redacted)";
}
/// <summary>Copied broker command arguments. Rust owns resolver, socket and retry lifetimes.</summary>
public sealed record NativeOwnerBroker(string Endpoint, string[] DnsServers)
{
    /// <summary>Broker URLs may contain credentials; do not expose them in logs.</summary>
    public override string ToString() => "Native broker configuration (credentials redacted)";
}
/// <summary>Copied background source intent. Quiet is explicit session evidence;
/// the native owner performs discovery, dependency acquisition and recovery.</summary>
public sealed record NativeOwnerBackground(string Environment,bool Quiet=false,bool Committed=false);
/// <summary>Copied preparation provenance; native committed-authority handoff
/// retires this provisional demand regardless of physical dataset generation.</summary>
public sealed record NativePreparationOrigin(string Workflow,long Revision,string Input);

/// <summary>Copied business scope intent. Rust derives all physical membership
/// and interprets optional expiry with its authoritative API clock.</summary>
public sealed record NativeOwnerRawScope(Guid Owner,string Scope,bool FullRisk,string? Horizon=null,DateTimeOffset? ExpiresAtUtc=null);

/// <summary>Desired prepared-cache membership. It is not a managed subscription or renewal owner.</summary>
public sealed record NativeOwnerCacheScope(string Scope, string Horizon, string[] Contracts, NativeOwnerRanking? Ranking = null);
/// <summary>Copied global policy input; Rust validates, selects and owns it.</summary>
public sealed record NativeOwnerRanking(TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet Parameters,
    TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.OptionStrategyMarketBias Direction, DateOnly ValueDate);

/// <summary>Raw configured root demand, resolved and rebuilt entirely in Rust.</summary>
public sealed record NativeOwnerFuturesRoot(string Symbol,uint Count,string? CurrencyFallback,bool Vwap);

/// <summary>Raw endpoint configuration only. Rust constructs plans, dependencies and registrations.</summary>
public sealed record NativeOwnerGateway(string Key,string Host,ushort Port,string[] DnsServers)
{
    /// <summary>Never include provider credentials in incidental diagnostics.</summary>
    public override string ToString()=>"Native gateway configuration (credentials redacted)";
}
/// <summary>Immutable configuration facts. No individual prepared context,
/// calendar lookup, lease, renewal task or source connection is represented here.</summary>
public sealed record NativeOwnerReferencePolicy(
    TomasAI.IFM.Application.MarketData.Pricing.TreasuryPublicationPolicy Publication,
    TomasAI.IFM.Shared.MarketData.Contracts.TreasuryRateConversionPolicy Conversion,
    TomasAI.IFM.Application.MarketData.Pricing.OptionPricingRefreshPolicy Refresh,
    bool Production=false,int MaximumSourceClockLeadMilliseconds=0);

public sealed partial class NativeMarketDataRuntime
{
    static int OwnerDate(DateOnly date) => date.Year * 10000 + date.Month * 100 + date.Day;
    static object OwnerSource(NativeOwnerSource source) => ScopeRequest(source.Key, source.Host, source.Port,
        source.DnsServers, source.Subscriptions, source.Registrations, source.Observations);
    static object OwnerBroker(NativeOwnerBroker broker) => new { endpoint = broker.Endpoint, dns = broker.DnsServers };

    /// <summary>Marshals explicit futures intent once. Rust owns readiness,
    /// resource setup and all subsequent recovery; this creates no C# control task.</summary>
    public void StartDatasetOwnerFutures(MarketDataLane lane, DateOnly valueDate, DateOnly definitionDate,
        string scope, int revision, NativeOwnerSource source, NativeOwnerBroker broker)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(broker);
        StartDatasetOwnerFutures(lane, JsonSerializer.SerializeToUtf8Bytes(new
        {
            date = OwnerDate(valueDate), definition = OwnerDate(definitionDate), scope, revision,
            source = OwnerSource(source), broker = OwnerBroker(broker)
        }));
    }
    /// <summary>Accepts native definition/bootstrap intent. No C# contract lookup,
    /// provider subscription, startup timer or managed recovery helper is started.
    /// ConfigureHistory must first supply the native provider HTTP endpoint.</summary>
    public void DiscoverDatasetOwnerFutures(MarketDataLane lane,DateOnly valueDate,DateOnly definitionDate,string scope,int revision,
        NativeOwnerSource template,NativeOwnerBroker broker,NativeOwnerFuturesRoot[] roots,bool sessionReplay=true)
        =>StartDatasetOwnerFutures(lane,JsonSerializer.SerializeToUtf8Bytes(new{
            date=OwnerDate(valueDate),definition=OwnerDate(definitionDate),scope,revision,source=OwnerSource(template),broker=OwnerBroker(broker),
            discover=new{roots=roots.Select(r=>new{symbol=r.Symbol,count=r.Count,fallback=r.CurrencyFallback,vwap=r.Vwap}),session_replay=sessionReplay}}));

    /// <summary>Marshals the complete raw configured futures demand as one native
    /// command. No C# provider loop, resolution or subscription lifecycle runs.</summary>
    public void StartDatasetOwnerFutures(DateOnly valueDate,DateOnly definitionDate,string scope,NativeOwnerBroker broker,
        TomasAI.IFM.Application.MarketData.Rust.Api.NativeOwnedFuturesDemand[] demands)
    {
        var command=JsonSerializer.SerializeToUtf8Bytes(demands.Select(d=>new{
            lane=(uint)d.Lane,date=OwnerDate(valueDate),definition=OwnerDate(definitionDate),scope,revision=1,
            source=OwnerSource(new NativeOwnerSource(d.Gateway.Key,d.Gateway.Host,d.Gateway.Port,d.Gateway.DnsServers,[],[])),broker=OwnerBroker(broker),
            discover=new{roots=d.Roots.Select(r=>new{symbol=r.Symbol,count=r.Count,fallback=r.CurrencyFallback,vwap=r.Vwap}),session_replay=d.SessionReplay}}));
        try{StartDatasetOwnerFutures(command);}finally{System.Security.Cryptography.CryptographicOperations.ZeroMemory(command);}
    }

    /// <summary>Copies reviewed raw reference inputs. Treasury acquisition,
    /// context preparation, refresh, claims and rebuilding happen inside Rust.</summary>
    public void ConfigureDatasetOwnerCatalog(DateOnly valueDate, DateOnly definitionDate, string scope,
        NativeOwnerSource source, NativeOwnerBroker broker, NativeReviewedScopeReferences references,
        TomasAI.IFM.Shared.MarketData.Contracts.OptionChainStatisticsSnapshot[]? statistics = null, bool referenceStore = false, TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel[]? rawContracts = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(broker); ArgumentNullException.ThrowIfNull(references);
        ConfigureDatasetOwnerCatalog(JsonSerializer.SerializeToUtf8Bytes(new
        {
            date = OwnerDate(valueDate), definition = OwnerDate(definitionDate), scope,
            source = OwnerSource(source), broker = OwnerBroker(broker), reference_store = referenceStore,
            raw_contracts = rawContracts?.Select(Publication.NativeReferenceProjection.Option),
            reviewed = ReviewedScopePayload(references, source.Registrations, statistics ?? [])
        }));
    }
    /// <summary>Marshals raw reviewed contracts and immutable configuration.
    /// Rust constructs each convention, pricing calendar, physical dependency and
    /// subscription plan, then owns lookup/staging and complete dataset recovery.</summary>
    public void ConfigureDatasetOwnerRawCatalog(DateOnly valueDate,DateOnly definitionDate,string scope,
        NativeOwnerGateway gateway,NativeOwnerBroker broker,NativeOwnerReferencePolicy referencePolicy,
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel[] contracts,
        TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner? monitoredOwner = null,NativeOwnerRawScope? scopeClaim = null,NativeOwnerBackground? background = null, TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner? canonicalOwner = null,string? canonicalContract = null,TomasAI.IFM.Application.MarketData.Pricing.CompositionDiscoveryRequest? discoveryRequest = null,NativePreparationOrigin? preparationOrigin = null)
    {
        ArgumentNullException.ThrowIfNull(gateway);ArgumentNullException.ThrowIfNull(broker);ArgumentNullException.ThrowIfNull(referencePolicy);ArgumentNullException.ThrowIfNull(contracts);
        var publication=referencePolicy.Publication;var conversion=referencePolicy.Conversion;var policy=referencePolicy.Refresh;
        static long Ticks(DateTimeOffset at)=>at.UtcTicks-DateTime.UnixEpoch.Ticks;
        static long Offset(DateTimeOffset at)=>(long)at.Offset.TotalMinutes;
        var command=JsonSerializer.SerializeToUtf8Bytes(new{
            raw_only=true,reference_store=true,merge=true,date=OwnerDate(valueDate),definition=OwnerDate(definitionDate),scope,
            source=new{key=gateway.Key,host=gateway.Host,port=gateway.Port,dns=gateway.DnsServers},broker=OwnerBroker(broker),
            raw_contracts=contracts.Select(Publication.NativeReferenceProjection.Option),
            canonical_scope=DiscoveryArguments(discoveryRequest,preparationOrigin,referencePolicy.MaximumSourceClockLeadMilliseconds),
            canonical_monitor=canonicalOwner is {} canonical ? new {workflow_type=canonical.WorkflowType,workflow_id=canonical.WorkflowId,leg_id=canonical.LegId,contract=canonicalContract}:null,
            claim=RawScopeArguments(scopeClaim),
            background=background is null?null:new{environment=background.Environment,quiet=background.Quiet,committed=background.Committed},
            monitor=monitoredOwner is {} owner ? new { workflow_type=owner.WorkflowType,workflow_id=owner.WorkflowId,leg_id=owner.LegId,contract=contracts.Length==1?contracts[0].ContractId.ToString():null } : null,
            reviewed=new{
                publication=new{version=publication.Version,timezone=publication.TimeZoneId,from=Ticks(publication.CoverageFromUtc),until=Ticks(publication.CoverageUntilUtc),from_offset=Offset(publication.CoverageFromUtc),until_offset=Offset(publication.CoverageUntilUtc),
                    entries=publication.Publications.Select(p=>new{date=p.ValueDate.DayNumber,available=Ticks(p.AvailableByUtc),offset=Offset(p.AvailableByUtc)})},
                conversion=new{source=conversion.Source,series=conversion.SourceSeriesId,convention=(int)conversion.Convention,version=conversion.Version,evidence=conversion.EvidenceId},
                maximum_lead=referencePolicy.MaximumSourceClockLeadMilliseconds,production=referencePolicy.Production,production_approved=policy.ProductionApproved,review_evidence=policy.ReviewEvidenceId,
                policy_version=policy.Version,selection_ms=policy.PriceDeltaMilliseconds,risk_ms=policy.FullRiskMilliseconds,iv_ms=policy.ImpliedVolatilityMilliseconds,batch=policy.MaximumContractsPerPass,maximum_contracts=policy.MaximumContracts,statistics=Array.Empty<object>()}});
        try{ConfigureDatasetOwnerCatalog(command);}finally{System.Security.Cryptography.CryptographicOperations.ZeroMemory(command);}
    }

    static object? DiscoveryArguments(TomasAI.IFM.Application.MarketData.Pricing.CompositionDiscoveryRequest? request,NativePreparationOrigin? preparationOrigin,int maximumLead)
    {
        if(request is null)return null;
        var bits=GuidBits(request.LeaseId);
        return new{owner=new[]{bits.Low,bits.High},maturity=request.MaturityDate.DayNumber,complete=request.ScopeComplete,
            origin=preparationOrigin is null?null:new{workflow=preparationOrigin.Workflow,revision=preparationOrigin.Revision,input=preparationOrigin.Input,
                until=request.DeadlineUtc.UtcTicks-DateTime.UnixEpoch.Ticks,until_offset=(long)request.DeadlineUtc.Offset.TotalMinutes},
            references=Publication.NativeReferenceProjection.References(request.Calendar,request.Publication,request.Conversion,maximumLead),
            separate=request.SeparateContractConnection,independent=request.IndependentChainConnection,
            candidates=Publication.NativeReferenceProjection.Candidates(request.Definitions)};
    }
    /// <summary>Accepts normalized immutable-version scope demand before storage
    /// acquisition. Native code owns qualification, source setup and recovery.</summary>
    public void ConfigureDatasetOwnerDiscovery(TomasAI.IFM.Application.MarketData.Pricing.CompositionDiscoveryRequest request,string scope,
        NativeOwnerGateway gateway,NativeOwnerBroker broker,NativeOwnerReferencePolicy policy,NativePreparationOrigin? preparationOrigin=null)
        =>ConfigureDatasetOwnerRawCatalog(request.ValueDate,request.ValueDate,scope,gateway,broker,
            policy with{Publication=request.Publication,Conversion=request.Conversion},[],discoveryRequest:request,preparationOrigin:preparationOrigin);
    /// <summary>Starts native-owned automatic policy/definition discovery with
    /// no caller-resolved contracts, calendar, contexts, leases or subscriptions.</summary>
    public void ConfigureDatasetOwnerBackground(DateOnly valueDate,DateOnly definitionDate,string scope,
        NativeOwnerGateway gateway,NativeOwnerBroker broker,NativeOwnerReferencePolicy policy,NativeOwnerBackground background)
    {
        ArgumentNullException.ThrowIfNull(background);
        ConfigureDatasetOwnerRawCatalog(valueDate,definitionDate,scope,gateway,broker,policy,[],background:background);
    }
    /// <summary>Releases only the copied workflow/leg/contract business identity.
    /// Rust removes its claim and retains dependencies still used by other owners.
    /// No managed lease token or physical generation is stored by the caller.</summary>
    public void ReleaseDatasetOwnerOption(TomasAI.IFM.Shared.MarketData.Contracts.Ticker.TickerStreamOwner owner,string contract)
        =>CommandDatasetOwner(JsonSerializer.SerializeToUtf8Bytes(new{command="unmonitor",workflow_type=owner.WorkflowType,workflow_id=owner.WorkflowId,leg_id=owner.LegId,contract}));

    /// <summary>Copies complete desired membership. Configuration does not start
    /// or renew physical subscriptions; native business claims select the union.</summary>
    public void ConfigureDatasetOwnerScope(string scope, uint[] options, uint[] monitored, NativeOwnerCacheScope[] caches)
        => CommandDatasetOwner(JsonSerializer.SerializeToUtf8Bytes(new { command = "configure", id = scope, options, monitored,
            caches = caches.Select(c => new { scope = c.Scope, horizon = c.Horizon, contracts = c.Contracts, ranking = RankingArguments(c.Ranking) }) }));

    /// <summary>Atomically accepts complete scope membership and stable business
    /// ownership. Rust validates the whole union before changing desired state.
    /// A null expiry means durable intent; an explicit UTC expiry is interpreted
    /// against Rust's API clock, never the managed machine clock.</summary>
    public void ClaimDatasetOwnerScope(Guid owner,string scope,uint[] options,uint[] monitored,
        NativeOwnerCacheScope[] caches,DateTimeOffset? expiresAtUtc=null)
    {
        var bits=GuidBits(owner);
        CommandDatasetOwner(JsonSerializer.SerializeToUtf8Bytes(new{command="claim-scope",id=scope,
            owner=new[]{bits.Low,bits.High},options,monitored,
            expires_ticks=expiresAtUtc?.UtcTicks-DateTime.UnixEpoch.Ticks,
            expires_offset=expiresAtUtc?.Offset.TotalMinutes,
            caches=caches.Select(c=>new{scope=c.Scope,horizon=c.Horizon,contracts=c.Contracts,ranking=RankingArguments(c.Ranking)})}));
    }

    static object? RawScopeArguments(NativeOwnerRawScope? claim)
    {
        if(claim is null)return null;
        var bits=GuidBits(claim.Owner);
        return new{owner=new[]{bits.Low,bits.High},id=claim.Scope,full_risk=claim.FullRisk,horizon=claim.Horizon,
            expires_ticks=claim.ExpiresAtUtc?.UtcTicks-DateTime.UnixEpoch.Ticks,expires_offset=claim.ExpiresAtUtc?.Offset.TotalMinutes};
    }

    static object? RankingArguments(NativeOwnerRanking? ranking)
    {
        if(ranking is null)return null;
        return new{parameters=Publication.NativeReferenceProjection.Strategy(ranking.Parameters),direction=(int)ranking.Direction,date=OwnerDate(ranking.ValueDate)};
    }

    /// <summary>Enables a stable business owner. Durable intent needs no managed
    /// lease token, three-second renewal timer or caller-maintained provider handle.</summary>
    public void EnableDatasetOwner(Guid owner, string[] scopes)
    {
        var bits = GuidBits(owner);
        CommandDatasetOwner(JsonSerializer.SerializeToUtf8Bytes(new { command = "enable", owner = new[] { bits.Low, bits.High }, scopes }));
    }
    /// <summary>Disables only this business owner's claims. Shared dependencies
    /// remain active until Rust determines their last native owner has left.</summary>
    public void DisableDatasetOwner(Guid owner)
    {
        var bits = GuidBits(owner);
        CommandDatasetOwner(JsonSerializer.SerializeToUtf8Bytes(new { command = "disable", owner = new[] { bits.Low, bits.High } }));
    }
    /// <summary>Stops desired feeds; native controllers remain reusable.</summary>
    public void StopDatasetOwnerFeeds() => CommandDatasetOwner("{\"command\":\"stop\"}"u8);
}
