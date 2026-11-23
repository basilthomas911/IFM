using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Fact, Trait("Category", "PreparedWorkflowRuntime")]
    public async Task Real_host_resolves_one_local_runtime_and_cold_reads_do_not_fall_back_to_storage()
    {
        await using var host = Host(services =>
        {
            services.RemoveAll<IntrinsicTimeStrategyWorkflowOptions>();
            services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = false, LocalDecisionRuntimeEnabled = true });
        });
        _ = host.CreateClient();
        var runtime = host.Services.GetRequiredService<WorkflowCalculationRuntime>();
        var container = host.Services.GetRequiredService<SimpleInjector.Container>();
        var command = (IIntrinsicTimeStrategyWorkflowCommandContext)container.GetInstance<ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>>();
        var realtime = (IIntrinsicTimeStrategyWorkflowRealtimeContext)container.GetInstance<IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor>>();
        command.DecisionRuntime.Should().BeSameAs(runtime);
        realtime.DecisionRuntime.Should().BeSameAs(runtime);
        host.Services.GetRequiredService<WorkflowDecisionSession>().Id.Should().Be(runtime.SessionId);
        realtime.CompositionInputs.Should().BeSameAs(host.Services.GetRequiredService<WorkflowCompositionInputCache>());
        realtime.FinancialDecisionState.Should().BeSameAs(host.Services.GetRequiredService<IPortfolioDecisionStateReader>());
        var entity = TomasAI.IFM.Domain.Trade.Shared.IntrinsicTimeStrategyWorkflowEntityId.Create(
            new MarketData.Analytics.Shared.FuturesItiSignalEntityId("UNREGISTERED-QUALIFICATION", new(2026, 11, 20), MarketData.Analytics.Shared.TimeFrameType.Daily));
        (await realtime.ReadActiveWorkflowAsync(entity)).Should().BeNull();
        await global::SupervisorTestLifecycle.ShutdownAsync(host.Services);
    }
}
