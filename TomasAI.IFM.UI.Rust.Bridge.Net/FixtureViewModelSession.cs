using System.Reflection;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.Analytics;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Offline service boundary driving the unchanged production StrategyOperationsViewModel.</summary>
public sealed class FixtureViewModelSession : IDisposable
{
    readonly StrategyOperationsViewModel viewModel;
    Action<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent>? notify;
    public FixtureViewModelSession()
    {
        var query=FixtureProxy.Make<IMarketDataAnalyticsQueryApi>((m,a)=>m.Name switch
        {
            "GetFuturesItiSignalHistoryAsync"=>Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(new ServiceOk<FuturesItiSignalV2ReadModel[]>([])),
            "GetFuturesItiSignalAsync"=>Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel>>(new ServiceOk<FuturesItiSignalV2ReadModel>(new())),
            _=>throw new NotSupportedException(m.Name)
        });
        var workflows=FixtureProxy.Make<IIntrinsicTimeStrategyWorkflowQueryApi>((m,a)=>m.Name switch
        {
            "GetHistoryPageAsync"=>Task.FromResult<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryPageReadModel>>(new ServiceOk<IntrinsicTimeStrategyWorkflowHistoryPageReadModel>(new([],1,50,0))),
            _=>throw new NotSupportedException(m.Name)
        });
        var signals=FixtureProxy.Make<IFuturesItiSignalUIEventConsumer>((m,a)=>ValueTask.CompletedTask);
        var events=FixtureProxy.Make<IIntrinsicTimeStrategyWorkflowUIEventConsumer>((m,a)=>{if(m.Name=="StartAsync")notify=(Action<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent>)a![1]!;return ValueTask.CompletedTask;});
        var service=StrategyOperationsComposition.Create(query,signals,workflows,events);
        viewModel=StrategyOperationsViewModelComposition.Create(service,"ES","ESZ26",DateOnly.FromDateTime(DateTime.UtcNow));
        WithoutContext(()=>viewModel.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult());
    }
    public StrategyWorkflowDetails Select(IntrinsicTimeStrategyWorkflowView view)
    {
        notify!(new(){State=view,WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision,EntityId=view.EntityId});
        viewModel.SelectWorkflow(view.WorkflowId);
        return viewModel.SelectedWorkflowDetails??throw new InvalidOperationException("Fixture workflow did not reach the production view model");
    }
    public void Dispose()=>WithoutContext(()=>viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult());
    static void WithoutContext(Action work){var previous=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(null);try{work();}finally{SynchronizationContext.SetSynchronizationContext(previous);}}
}
public class FixtureProxy : DispatchProxy
{
    public Func<MethodInfo,object?[]?,object?> Handler {get;set;}=null!;
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>Handler(method!,args);
    public static T Make<T>(Func<MethodInfo,object?[]?,object?> handler) where T:class {var proxy=Create<T,FixtureProxy>();((FixtureProxy)(object)proxy).Handler=handler;return proxy;}
}
