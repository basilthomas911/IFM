using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Strategy.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using Xunit;
using TomasAI.IFM.Domain.Trade.Shared;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
public sealed class WorkflowConfigurationCacheTests
{
    [Fact]
    public async Task Cache_coalesces_catalog_policy_reads_and_freezes_distinct_workflows_then_invalidates()
    {
        var original=await TradeSelectionFixture.Command("LongFuture");var b=original.SelectionBinding;
        var p=CommonPolicy(b);var at=b.FrozenAtUtc;
        var activation=new TradeSelectionActivation{SchemaVersion=2,ParameterSetId=Guid.NewGuid(),Version=1,InstrumentRoot="ES",
            TargetHorizon=p.TargetHorizon,SelectionPolicyReference=b.CommonPolicy,DeploymentKeys=b.DeploymentSnapshots.Select(x=>x.DeploymentKey).ToArray()};
        var config=Substitute.For<IConfigurationDbContext>();var reader=Substitute.For<IStrategyDeploymentReader>();
        config.GetEffectiveTradeSelectionActivationAsync(activation.ParameterSetId,1,activation.Hash(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(activation);
        config.GetEffectiveTradeSelectionVersionAsync(p.ParameterSetId,p.Version,b.CommonPolicy.PayloadSha256,Arg.Any<DateTime>(),Arg.Any<CancellationToken>())
            .Returns(new ResolvedTradeSelectionParameterSet(p,b.CommonPolicy.PayloadSha256,ConfigurationParameterSetStatus.Published,at.AddDays(-1),null));
        foreach(var policy in b.PipelinePolicies)
            config.GetSelectionPipelinePolicyAsync(policy.Kind,policy.Id,policy.Version,Arg.Any<CancellationToken>()).Returns(policy);
        foreach(var graph in b.DeploymentSnapshots)
        {
            var definitions=b.CatalogDefinitions.Where(x=>graph.DefinitionKeys.Contains(x.Key)).Select(SelectionCatalogTransport.ToSource).ToArray();
            reader.ReadPublishedAsync(config,graph.DeploymentKey,Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(call=>
                new StrategyCatalogSnapshot(graph.DeploymentKey,call.Arg<DateTime>(),definitions,graph.ContentHash));
        }
        var options=new IntrinsicTimeStrategyWorkflowOptions{Activations=[new(p.TargetHorizon,activation.ParameterSetId,1,activation.Hash())]};
        var view=original.WorkflowView with{SelectionBinding=null,StartedAtUtc=at,ExpiresAtUtc=at.AddMinutes(5),EntityId=original.WorkflowEntityId with{Assignment=null}};
        var cache=new WorkflowSelectionConfigurationCache();
        var coldReads=config.ReceivedCalls().Count();var coldGraphReads=reader.ReceivedCalls().Count();
        Assert.False(cache.TryResolveReady(config,options,view,out _));
        Assert.Equal(coldReads,config.ReceivedCalls().Count());Assert.Equal(coldGraphReads,reader.ReceivedCalls().Count());
        var first=await cache.ResolveAsync(config,reader,options,view);
        var reads=config.ReceivedCalls().Count();var graphReads=reader.ReceivedCalls().Count();
        var another=view with{WorkflowId=StrategyWorkflowId.New(TimeProvider.System),CorrelationId=Guid.NewGuid(),StartedAtUtc=at.AddSeconds(1)};
        Assert.True(cache.TryResolveReady(config,options,another,out var prepared));
        Assert.Equal(another.WorkflowId.Value,prepared!.StrategyUniverse!.WorkflowId);
        var hits=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>cache.ResolveAsync(config,reader,options,another)));
        Assert.Equal(reads,config.ReceivedCalls().Count());Assert.Equal(graphReads,reader.ReceivedCalls().Count());
        Assert.Equal(view.WorkflowId.Value,first.StrategyUniverse!.WorkflowId);
        Assert.All(hits,x=>{ValidateBinding(x);Assert.Equal(another.WorkflowId.Value,x.StrategyUniverse!.WorkflowId);Assert.NotEqual(first.PayloadSha256,x.PayloadSha256);});
        var frozenHash=first.PayloadSha256;
        ReadCacheInvalidation.ConfigurationChanged();
        Assert.False(cache.TryResolveReady(config,options,another,out _));
        Assert.Equal(reads,config.ReceivedCalls().Count());Assert.Equal(graphReads,reader.ReceivedCalls().Count());
        await cache.ResolveAsync(config,reader,options,another);
        Assert.True(config.ReceivedCalls().Count()>reads);Assert.Equal(frozenHash,first.PayloadSha256);ValidateBinding(first);
    }
}
