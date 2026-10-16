using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Closed, typed parameter contract. Each variant contains its actual configuration, never an encoded document.</summary>
[Union(0,typeof(RegimeDiscoveryParameterValue))]
[Union(1,typeof(FuturesItiSignalParameterValue))]
[Union(2,typeof(OptionVolatilitySeriesParameterValue))]
[Union(3,typeof(OptionVolatilityConsumerRulesParameterValue))]
[Union(4,typeof(OptionVolatilityRetentionParameterValue))]
[Union(5,typeof(IronCondorMarketSelectionParameterValue))]
[Union(6,typeof(VerticalSpreadMarketSelectionParameterValue))]
[Union(7,typeof(StrategyOptionChainParameterValue))]
public abstract record ParameterSetValue
{
    [IgnoreMember] public abstract Guid SetId {get;}
    [IgnoreMember] public abstract int Version {get;}
    [IgnoreMember] public abstract int SchemaVersion {get;}
    [IgnoreMember] public abstract string ComponentCode {get;}
    public abstract ParameterSetValue WithIdentity(Guid setId,int version);
    public T Get<T>() => this switch
    {
        RegimeDiscoveryParameterValue x when x.Value is T value=>value,
        FuturesItiSignalParameterValue x when x.Value is T value=>value,
        OptionVolatilitySeriesParameterValue x when x.Value is T value=>value,
        OptionVolatilityConsumerRulesParameterValue x when x.Value is T value=>value,
        OptionVolatilityRetentionParameterValue x when x.Value is T value=>value,
        IronCondorMarketSelectionParameterValue x when x.Value is T value=>value,
        VerticalSpreadMarketSelectionParameterValue x when x.Value is T value=>value,
        StrategyOptionChainParameterValue x when x.Value is T value=>value,
        _=>throw new ArgumentException("Parameter type does not match the selected component")
    };

    public static ParameterSetValue From(RegimeDiscoveryParameterSet value) => new RegimeDiscoveryParameterValue(value);
    public static ParameterSetValue From(FuturesItiSignalParameterSet value) => new FuturesItiSignalParameterValue(value);
    public static ParameterSetValue From(OptionVolatilitySeriesParameterSet value) => new OptionVolatilitySeriesParameterValue(value);
    public static ParameterSetValue From(OptionVolatilityConsumerRulesParameterSet value) => new OptionVolatilityConsumerRulesParameterValue(value);
    public static ParameterSetValue From(OptionVolatilityRetentionParameterSet value) => new OptionVolatilityRetentionParameterValue(value);
    public static ParameterSetValue From(IronCondorMarketSelectionParameterSet value) => new IronCondorMarketSelectionParameterValue(value);
    public static ParameterSetValue From(VerticalSpreadMarketSelectionParameterSet value) => new VerticalSpreadMarketSelectionParameterValue(value);
    public static ParameterSetValue From(StrategyOptionChainParameterSet value) => new StrategyOptionChainParameterValue(value);
}

[MessagePackObject]
public sealed record RegimeDiscoveryParameterValue([property: Key(0)] RegimeDiscoveryParameterSet Value) : ParameterSetValue
{
    [IgnoreMember] public override Guid SetId=>Value.ParameterSetId;
    [IgnoreMember] public override int Version=>Value.Version;
    [IgnoreMember] public override int SchemaVersion=>Value.SchemaVersion;
    [IgnoreMember] public override string ComponentCode=>ParameterSchemaRegistry.RegimeComponent;
    public override ParameterSetValue WithIdentity(Guid setId,int version)=>this with{Value=Value with{ParameterSetId=setId,Version=version}};
}

[MessagePackObject]
public sealed record FuturesItiSignalParameterValue([property: Key(0)] FuturesItiSignalParameterSet Value) : ParameterSetValue
{
    [IgnoreMember] public override Guid SetId=>Value.ParameterSetId;
    [IgnoreMember] public override int Version=>Value.Version;
    [IgnoreMember] public override int SchemaVersion=>Value.SchemaVersion;
    [IgnoreMember] public override string ComponentCode=>ParameterSchemaRegistry.FuturesItiSignalComponent;
    public override ParameterSetValue WithIdentity(Guid setId,int version)=>this with{Value=Value with{ParameterSetId=setId,Version=version}};
}

[MessagePackObject]
public sealed record OptionVolatilitySeriesParameterValue([property: Key(0)] OptionVolatilitySeriesParameterSet Value) : ParameterSetValue
{
    [IgnoreMember] public override Guid SetId=>Value.ParameterSetId;
    [IgnoreMember] public override int Version=>Value.Version;
    [IgnoreMember] public override int SchemaVersion=>Value.SchemaVersion;
    [IgnoreMember] public override string ComponentCode=>ParameterSchemaRegistry.OptionVolatilitySeriesComponent;
    public override ParameterSetValue WithIdentity(Guid setId,int version)=>this with{Value=Value with{ParameterSetId=setId,Version=version}};
}

[MessagePackObject]
public sealed record OptionVolatilityConsumerRulesParameterValue([property: Key(0)] OptionVolatilityConsumerRulesParameterSet Value) : ParameterSetValue
{
    [IgnoreMember] public override Guid SetId=>Value.ParameterSetId;
    [IgnoreMember] public override int Version=>Value.Version;
    [IgnoreMember] public override int SchemaVersion=>Value.SchemaVersion;
    [IgnoreMember] public override string ComponentCode=>ParameterSchemaRegistry.OptionVolatilityConsumerRulesComponent;
    public override ParameterSetValue WithIdentity(Guid setId,int version)=>this with{Value=Value with{ParameterSetId=setId,Version=version}};
}

[MessagePackObject]
public sealed record OptionVolatilityRetentionParameterValue([property: Key(0)] OptionVolatilityRetentionParameterSet Value) : ParameterSetValue
{
    [IgnoreMember] public override Guid SetId=>Value.ParameterSetId;
    [IgnoreMember] public override int Version=>Value.Version;
    [IgnoreMember] public override int SchemaVersion=>Value.SchemaVersion;
    [IgnoreMember] public override string ComponentCode=>ParameterSchemaRegistry.OptionVolatilityRetentionComponent;
    public override ParameterSetValue WithIdentity(Guid setId,int version)=>this with{Value=Value with{ParameterSetId=setId,Version=version}};
}

[MessagePackObject]
public sealed record IronCondorMarketSelectionParameterValue([property: Key(0)] IronCondorMarketSelectionParameterSet Value) : ParameterSetValue
{
    [IgnoreMember] public override Guid SetId=>Value.ParameterSetId;
    [IgnoreMember] public override int Version=>Value.Version;
    [IgnoreMember] public override int SchemaVersion=>Value.SchemaVersion;
    [IgnoreMember] public override string ComponentCode=>ParameterSchemaRegistry.IronCondorMarketSelectionComponent;
    public override ParameterSetValue WithIdentity(Guid setId,int version)=>this with{Value=Value with{ParameterSetId=setId,Version=version}};
}

[MessagePackObject]
public sealed record VerticalSpreadMarketSelectionParameterValue([property: Key(0)] VerticalSpreadMarketSelectionParameterSet Value) : ParameterSetValue
{
    [IgnoreMember] public override Guid SetId=>Value.ParameterSetId;
    [IgnoreMember] public override int Version=>Value.Version;
    [IgnoreMember] public override int SchemaVersion=>Value.SchemaVersion;
    [IgnoreMember] public override string ComponentCode=>ParameterSchemaRegistry.VerticalSpreadMarketSelectionComponent;
    public override ParameterSetValue WithIdentity(Guid setId,int version)=>this with{Value=Value with{ParameterSetId=setId,Version=version}};
}

[MessagePackObject]
public sealed record StrategyOptionChainParameterValue([property: Key(0)] StrategyOptionChainParameterSet Value) : ParameterSetValue
{
    [IgnoreMember] public override Guid SetId=>Value.ParameterSetId;
    [IgnoreMember] public override int Version=>Value.Version;
    [IgnoreMember] public override int SchemaVersion=>Value.SchemaVersion;
    [IgnoreMember] public override string ComponentCode=>ParameterSchemaRegistry.StrategyOptionChainCacheComponent;
    public override ParameterSetValue WithIdentity(Guid setId,int version)=>this with{Value=Value with{ParameterSetId=setId,Version=version}};
}

