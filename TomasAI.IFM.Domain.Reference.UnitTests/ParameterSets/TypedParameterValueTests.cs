using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class TypedParameterValueTests
{
    [Fact]
    public void Current_mutations_require_concrete_values_instead_of_json_documents()
    {
        var command=new CreateParameterSetCommand{EntityId=new(Guid.NewGuid()),Name="Retired input",PayloadJson="{}"};
        var error=Assert.Throws<ArgumentException>(()=>ParameterMutationModel.RequestHash(command));
        Assert.Contains("PARAM.TYPED_VALUES_REQUIRED",error.Message);
    }

    public static IEnumerable<object[]> Components()=>new IParameterComponentDescriptor[]
    {
        new RegimeDiscoveryParameterModel(),new FuturesItiSignalParameterModel(),
        new OptionVolatilitySeriesParameterModel(),new OptionVolatilityConsumerRulesParameterModel(),
        new OptionVolatilityRetentionParameterModel(),new IronCondorMarketSelectionParameterModel(),
        new VerticalSpreadMarketSelectionParameterModel(),new StrategyOptionChainParameterModel()
    }.Select(x=>new object[]{x.Summary.ComponentCode});

    [Theory][MemberData(nameof(Components))]
    public void Every_parameter_family_round_trips_as_its_concrete_type_without_document_text(string component)
    {
        var descriptor=ParameterComponentModelRegistry.Get(component);
        var parameters=descriptor.CreateTypedDraft(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef")).WithIdentity(Guid.NewGuid(),7);
        Assert.Empty(descriptor.ValidateTyped(parameters,parameters.SchemaVersion));
        var bytes=ParameterValueCodec.Encode(parameters);
        var restored=ParameterValueCodec.Decode(bytes);
        Assert.Equal(parameters.GetType(),restored.GetType());
        Assert.Equal(parameters.SetId,restored.SetId);
        Assert.Equal(7,restored.Version);
        Assert.Equal(component,restored.ComponentCode);
        Assert.Equal(bytes,ParameterValueCodec.Encode(restored));
        Assert.Equal(ParameterValueCodec.Hash(parameters),ParameterValueCodec.Hash(restored));
        Assert.DoesNotContain("PayloadJson",System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Snapshot_detaches_arrays_and_preserves_exact_decimal_values()
    {
        var parameters=ParameterSetValue.From(new IronCondorMarketSelectionParameterSet
        {
            ParameterSetId=Guid.NewGuid(),Symbols=[new(){Symbol="ES",CallSpreadWidth=12345678901234567890.123456789m,PutSpreadWidth=50}]
        });
        var frozen=ParameterValueCodec.Snapshot(parameters);
        var original=parameters.Get<IronCondorMarketSelectionParameterSet>();
        var copy=frozen.Get<IronCondorMarketSelectionParameterSet>();
        Assert.Equal(original.Symbols[0].CallSpreadWidth,copy.Symbols[0].CallSpreadWidth);
        original.Symbols[0]=original.Symbols[0] with{CallSpreadWidth=25};
        Assert.NotEqual(original.Symbols[0].CallSpreadWidth,copy.Symbols[0].CallSpreadWidth);
        Assert.Throws<ArgumentException>(()=>frozen.Get<FuturesItiSignalParameterSet>());
    }

    [Fact]
    public void Typed_validation_retains_specialist_business_rules()
    {
        var descriptor=new FuturesItiSignalParameterModel();
        var value=new FuturesItiSignalParameterSet{ParameterSetId=Guid.NewGuid(),DefaultTradingDays=new(){Daily=0,Weekly=20,Monthly=60}};
        Assert.Contains(descriptor.ValidateTyped(ParameterSetValue.From(value),1),x=>x.Path=="DefaultTradingDays/Daily");
        Assert.Contains(descriptor.ValidateTyped(ParameterSetValue.From(value),2),x=>x.Code=="PARAM.SCHEMA_MISMATCH");
        var regime=new RegimeDiscoveryParameterModel();
        Assert.Contains(regime.ValidateTyped(ParameterSetValue.From(value),1),x=>x.Code=="PARAM.SCHEMA_MISMATCH");
    }

    [Theory][MemberData(nameof(Components))]
    public void Component_mismatch_is_reported_without_throwing(string component)
    {
        var descriptor=ParameterComponentModelRegistry.Get(component);
        var wrong=component==ParameterSchemaRegistry.FuturesItiSignalComponent
            ? ParameterSetValue.From(new IronCondorMarketSelectionParameterSet{ParameterSetId=Guid.NewGuid()})
            : ParameterSetValue.From(new FuturesItiSignalParameterSet{ParameterSetId=Guid.NewGuid()});
        Assert.Contains(descriptor.ValidateTyped(wrong,wrong.SchemaVersion),x=>x.Severity==ParameterIssueSeverity.Error);
    }
}
