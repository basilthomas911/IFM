using MessagePack;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query;
using TomasAI.IFM.Domain.Reference.ParameterSets.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class TypedParameterQueryTests
{
    [Theory][MemberData(nameof(TypedParameterValueTests.Components),MemberType=typeof(TypedParameterValueTests))]
    public async Task Binary_queries_preview_and_validate_every_concrete_family(string component)
    {
        var context=Substitute.For<IParameterSetQueryContext>();
        context.AccessPolicy.Returns(Substitute.For<IParameterAccessPolicy>());
        context.Logger.Returns(Substitute.For<ILogger<ParameterSetQueryActor>>());
        var id=Guid.NewGuid();
        var query=MessagePackSerializer.Deserialize<PreviewTypedParameterDraftQuery>(MessagePackSerializer.Serialize(
            new PreviewTypedParameterDraftQuery { SetId=id,ComponentCode=component,Subject=new(ActorType.Query,
                PreviewTypedParameterDraftQuery.Actor,PreviewTypedParameterDraftQuery.Verb,ActorEntityId.Default.Format()) }));
        await query.ExecuteAsync(context,context.Logger,CancellationToken.None);
        await context.Received(1).ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,
            Arg.Is<ServiceResult<ParameterSetValue>>(result=>result.Success&&result.Value!.SetId==id&&result.Value.ComponentCode==component));
        var value=TypedParameterDraftModel.Preview(id,component,TimeFrameType.Daily);
        var validation=MessagePackSerializer.Deserialize<ValidateTypedParameterCandidateQuery>(MessagePackSerializer.Serialize(
            new ValidateTypedParameterCandidateQuery { Parameters=value,ComponentCode=component,SchemaVersion=value.SchemaVersion,
                Subject=new(ActorType.Query,ValidateTypedParameterCandidateQuery.Actor,ValidateTypedParameterCandidateQuery.Verb,ActorEntityId.Default.Format()) }));
        await validation.ExecuteAsync(context,context.Logger,CancellationToken.None);
        await context.Received(1).ReplyAsync(validation.Subject.ThreadId,validation.Subject.Verb,
            Arg.Is<ServiceResult<ParameterValidationReport>>(result=>result.Success&&result.Value!.CandidateSha256==ParameterValueCodec.Hash(value)));
    }

    [Fact]
    public void Preview_preserves_horizon_identity_and_detaches_children()
    {
        var id=Guid.NewGuid();
        var source=TypedParameterDraftModel.Preview(id,ParameterSchemaRegistry.RegimeComponent,TimeFrameType.Weekly);
        Assert.Equal(TimeFrameType.Weekly,source.Get<RegimeDiscoveryParameterSet>().TargetHorizon);
        var copy=TypedParameterDraftModel.Preview(id,source.ComponentCode,TimeFrameType.Weekly,source);
        Assert.NotSame(source.Get<RegimeDiscoveryParameterSet>().SignalMetrics,copy.Get<RegimeDiscoveryParameterSet>().SignalMetrics);
        Assert.Throws<ArgumentException>(()=>TypedParameterDraftModel.Preview(Guid.NewGuid(),source.ComponentCode,TimeFrameType.Weekly,source));
        Assert.Throws<ArgumentException>(()=>TypedParameterDraftModel.Preview(id,ParameterSchemaRegistry.FuturesItiSignalComponent,TimeFrameType.Daily,source));
        Assert.Throws<ArgumentOutOfRangeException>(()=>TypedParameterDraftModel.Preview(id,source.ComponentCode,(TimeFrameType)999));
    }

    [Fact]
    public void Missing_required_values_are_reported_before_specialist_validators_run()
    {
        var malformed=ParameterSetValue.From(new IronCondorMarketSelectionParameterSet{Symbols=null!});
        var report=TypedParameterDraftModel.Validate(malformed.ComponentCode,1,malformed);
        Assert.False(report.IsValid);
        Assert.Contains(report.Issues,x=>x.Code=="PARAM.VALUE_REQUIRED"&&x.Path=="Parameters/Symbols");
        var missingRoot=new IronCondorMarketSelectionParameterValue(null!);
        Assert.Contains(TypedParameterDraftModel.Validate(missingRoot.ComponentCode,1,missingRoot).Issues,x=>x.Path=="Parameters");
        Assert.False(TypedParameterDraftModel.Validate(malformed.ComponentCode,1,null).IsValid);
    }
}
