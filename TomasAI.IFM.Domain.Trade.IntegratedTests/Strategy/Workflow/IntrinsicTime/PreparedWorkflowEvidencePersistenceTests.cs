using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Theory, InlineData(false), InlineData(true), Trait("Category", "PreparedWorkflowRuntime")]
    public async Task Durable_admission_precedes_local_decisions_and_final_evidence_is_committed_without_an_intermediate_transaction(bool loseFinalReply)
    {
        await using var host = Host(services =>
        {
            services.RemoveAll<IntrinsicTimeStrategyWorkflowOptions>();
            services.AddSingleton(new IntrinsicTimeStrategyWorkflowOptions { Enabled = false, LocalDecisionRuntimeEnabled = true });
            // This fixture drives outcome reconciliation explicitly, without a competing background pass.
            foreach (var registration in services.Where(value => value.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                && value.ImplementationType == typeof(WorkflowRuntimeRecoveryService)).ToArray()) services.Remove(registration);
        });
        _ = host.CreateClient();
        try
        {
            var workflow = new Domain.Trade.Shared.StrategyWorkflowId(Guid.NewGuid());
            var container = host.Services.GetRequiredService<SimpleInjector.Container>();
            var context = (IIntrinsicTimeStrategyWorkflowCommandContext)container.GetInstance<ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>>();
            var transactions = new CountedTransactions(host.Services.GetRequiredService<IPostgresEventTransaction>(), loseFinalReply, workflow.Value);
            var registry = host.Services.GetRequiredService<WorkflowResidentStateRegistry>();
            var repository = new IntrinsicTimeStrategyWorkflowStateRepository(context.StateFactory, context.DbEventSource,
                context.ActorService, context.EventProjector, NullLogger<IntrinsicTimeStrategyWorkflowStateRepository>.Instance,
                context.DbFactory, transactions, registry, host.Services.GetRequiredService<WorkflowLocalDispatcher>(),
                host.Services.GetRequiredService<WorkflowDecisionBatchRunner>());
            var now = DateTime.UtcNow;
            var signal = new Domain.MarketData.Analytics.Shared.FuturesItiSignalEntityId(loseFinalReply ? "ES21991218" : "ES22991218", DateOnly.FromDateTime(now),
                Domain.MarketData.Analytics.Shared.TimeFrameType.Daily);
            var entity = Domain.Trade.Shared.IntrinsicTimeStrategyWorkflowEntityId.Create(signal);
            var trigger = Guid.NewGuid();
            var initial = new Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent
            {
                EntityId = entity, WorkflowRevision = 1, ReceivedOn = now, UpdatedAtUtc = now,
                Subject = new(ActorType.Event, "IntrinsicTimeStrategyWorkflow", "StateUpdated", entity.Format()),
                State = new() { EntityId = entity, WorkflowRevision = 1, Status = WorkflowStrategyMachineStatus.Started,
                    CurrentStage = StrategyWorkflowStage.RegimeDiscovery, WorkflowDefinitionVersion = 1, TriggerEventId = trigger, CorrelationId = Guid.NewGuid(),
                    TriggerEvent = new() { Id = trigger, EntityId = signal, CreatedOn = now, FuturesItiSignal = new()
                    { ContractId = signal.ContractId, ValueDate = DateOnly.FromDateTime(now), TimePeriod = signal.TimePeriod,
                        IntrinsicTime = now, IntrinsicPrice = 6500, SequenceId = 1,
                        IntrinsicTimeMode = Domain.MarketData.Analytics.Shared.IntrinsicTimeModeType.TrendDirectionChanged,
                        IntrinsicTimeTrend = Domain.MarketData.Analytics.Shared.IntrinsicTimeTrendType.UpTrend } },
                    RegimeDiscovery = new() { ProcessingStatus = StrategyActorProcessingStatus.Processing, InputWorkflowRevision = 1 } }
            };
            var command = new RedispatchCurrentStrategyPipelineCommand
            {
                CommandId = Guid.NewGuid(), EntityId = initial.EntityId, WorkflowId = workflow, ExpectedWorkflowRevision = 1,
                ExpectedStage = StrategyWorkflowStage.RegimeDiscovery, RequestedAtUtc = now, RequestedBy = "PreparedRuntimeQualification",
                Subject = new(ActorType.Command, RedispatchCurrentStrategyPipelineCommand.Actor, RedispatchCurrentStrategyPipelineCommand.Verb, initial.EntityId.Format())
            };
            var view = initial.State with { WorkflowId = workflow, DecisionRuntimeVersion = 1,
                DecisionRuntimeSessionId = context.DecisionRuntime!.SessionId, StartedAtUtc = now, UpdatedAtUtc = now,
                ExpiresAtUtc = now.AddSeconds(10), RegimeDiscovery = initial.State.RegimeDiscovery with { StartedAtUtc = now, ExpiresAtUtc = now.AddSeconds(2) } };
            var source = initial with { Id = Guid.NewGuid(), CommandId = command.CommandId, WorkflowId = workflow,
                State = view, DispatchRegimeDiscovery = true };
            var state = new IntrinsicTimeStrategyWorkflowCommandState { Id = command.Subject.ThreadId };
            state.Apply(source, addEvent: true).Should().BeTrue();

            if (loseFinalReply)
            {
                Func<Task> commit = async () => await repository.SaveStateAsync(context, state, command);
                await commit.Should().ThrowAsync<IOException>().WithMessage("Lost final evidence reply after PostgreSQL COMMIT.");
                registry.UncertainOutcomes().Should().ContainSingle(value => value.View.WorkflowId == workflow);
            }
            else await repository.SaveStateAsync(context, state, command);

            transactions.Count.Should().Be(2, "one admission and one final evidence transaction, with no stage transaction or query fallback");
            var saved = await context.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(workflow);
            saved.Should().NotBeNull();
            var restored = TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared
                .Deserialize<IntrinsicTimeStrategyWorkflowView>(saved!.StatePayload)!;
            restored.Status.Should().NotBe(WorkflowStrategyMachineStatus.Started);
            restored.EntryCommit.Should().BeNull();
            restored.RegimeDiscovery.Failure.Should().NotBeNull("cold inputs end immediately rather than loading data");
            if (loseFinalReply)
            {
                var recovery = new WorkflowRuntimeRecoveryService(host.Services.GetRequiredService<IPostgresEventTransaction>(),
                    host.Services.GetRequiredService<WorkflowLocalDispatcher>(), registry,
                    new IntrinsicTimeStrategyWorkflowOptions(), NullLogger<WorkflowRuntimeRecoveryService>.Instance);
                await recovery.ReconcileUncertainAsync(default);
                registry.UncertainOutcomes().Should().BeEmpty();
                state = await repository.LoadStateAsync(command);
                state.CurrentView!.WorkflowRevision.Should().Be(restored.WorkflowRevision);
                transactions.Count.Should().Be(2, "recovery reads the committed outcome without rerunning or writing another decision");
            }
            state.Events.Should().BeEmpty();
            state.PersistedStreamVersion.Should().BeGreaterThanOrEqualTo(2);
            // Every committed admission must reach the projector even though calculations bypass it.
            // Otherwise its earlier stream version permanently fences every queued final snapshot.
            var deadline = DateTime.UtcNow.AddSeconds(30);
            long outstanding;
            do
            {
                outstanding = await host.Services.GetRequiredService<IPostgresEventTransaction>().ExecuteOnceAsync(async (db, ct) =>
                    Convert.ToInt64(await db.ScalarAsync("""
                        SELECT count(*) FROM event_projector_state p JOIN event_stream_id s USING(eventstreamid)
                        WHERE s.eventstream=$1 AND p.projectorname='IntrinsicTimeStrategyWorkflowEventProjector'
                          AND (p.streamversion=1 OR $2) AND p.outcome NOT IN ('Completed','AlreadyCompleted','Superseded');
                        """, [command.StreamId, !loseFinalReply], ct)));
                if (outstanding == 0) break;
                await Task.Delay(100);
            } while (DateTime.UtcNow < deadline);
            outstanding.Should().Be(0, "committed admission cannot be left unpublished behind final decision evidence");
        }
        finally { await global::SupervisorTestLifecycle.ShutdownAsync(host.Services); }
    }

    sealed class CountedTransactions(IPostgresEventTransaction inner, bool loseFinalReply, Guid workflow) : IPostgresEventTransaction
    {
        internal int Count;
        public async Task<T> ExecuteOnceAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref Count);
            var result = await inner.ExecuteOnceAsync(operation, cancellationToken);
            if (current == 1)
            {
                var recovery = await inner.ExecuteOnceAsync((db, token) => db.QueryAsync(
                    global::TomasAI.IFM.Application.Storage.TradeDb.TradeDbWorkflowRuntimeSql.ReadActiveDecisionRecoveryPage, [Guid.Empty],
                    row => row.GetGuid(0), token), cancellationToken);
                recovery.Should().Contain(workflow, "restart scanning uses the stored Running text status and sees durable admission");
            }
            if (loseFinalReply && current == 2) throw new IOException("Lost final evidence reply after PostgreSQL COMMIT.");
            return result;
        }
        public Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Decision persistence must never select the retrying transaction API.");
    }
}
