using MessagePack;
using TomasAI.IFM.Application.EventSourcing.Persistence.Command;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Fund.Command;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

[Collection(WorkflowDecisionQualificationCollection.Name)]
[Trait("Category","DisposablePostgres")]
public sealed class WorkflowPublicationPostgresTests
{
    [Fact]
    public async Task Actual_workflow_owner_lost_reply_restart_and_legacy_source_proof_use_exact_Postgres_receipts()
    {
        await using var infrastructure=new IsolatedIntegrationInfrastructure("workflowpublication",includeCql:false);
        await infrastructure.StartAsync();
        var settings=new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,infrastructure.PostgresConnectionString,"System.Data.Postgres")
            .Add(TradeDbContext.TradeDbConnection,infrastructure.PostgresConnectionString,"System.Data.Postgres");
        await new EventSourceSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new TradeSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var transactions=new PostgresEventTransaction(settings);var authority=new WorkflowPublicationOwner(transactions);
        var source=WorkflowDispatchDeliveryFixture.RegimeFunctionInput(representative:true);
        var view=source.State with{DecisionRuntimeVersion=0};source=source with{State=view,CommandId=Guid.NewGuid()};
        var command=new FailRegimeDiscoveryCommand{CommandId=Guid.NewGuid(),EntityId=view.EntityId,WorkflowId=view.WorkflowId,
            InputWorkflowRevision=view.WorkflowRevision,SourceEventId=Guid.NewGuid(),FailedAtUtc=view.UpdatedAtUtc,
            Failure=new(){ErrorCode=123,ErrorMessage="Captured original failure"},Subject=new(ActorType.Command,FailRegimeDiscoveryCommand.Actor,FailRegimeDiscoveryCommand.Verb,view.EntityId.Format())};
        await transactions.ExecuteOnceAsync(async(tx,ct)=>{await tx.AppendAsync(new(command.StreamId,source.CommandId,source,0),ct);await tx.WorkflowProjectAsync(source,ct);return true;});
        var state=new IntrinsicTimeStrategyWorkflowCommandState();Assert.True(state.Apply(source,addEvent:false));
        var owner=Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();owner.TimeProvider.Returns(new Clock(view.UpdatedAtUtc));
        owner.Logger.Returns(NullLogger<IntrinsicTimeStrategyWorkflowCommandActor>.Instance);
        var root=Path.Combine(Path.GetTempPath(),"ifm-workflow-owner-pg-"+Guid.NewGuid().ToString("N"));
        using(var journal=new WorkflowPublicationJournal(root))
        {
            await using var fixture=new WorkflowDispatchDeliveryFixture(publications:journal,publicationOwner:authority);fixture.Time.At=view.UpdatedAtUtc;
            fixture.Send=async sent=>
            {
                var actual=Assert.IsType<FailRegimeDiscoveryCommand>(sent);Assert.True(actual.Execute(owner,state).Success);
                await transactions.ExecuteOnceAsync(async(tx,ct)=>
                {
                    await tx.ReserveCommandAsync(actual,ct);long revision=1;
                    foreach(var change in state.Events.OfType<WorkflowStrategyStateUpdatedEvent>())
                    {await tx.AppendAsync(new(actual.StreamId,actual.CommandId,change,revision++),ct);await tx.WorkflowProjectAsync(change,ct);}
                    return true;
                });
                throw new IOException("COMMIT succeeded; reply lost");
            };
            await Assert.ThrowsAsync<IOException>(()=>WorkflowPublicationDelivery.PublishAsync(fixture.Context,source,command).AsTask());
            Assert.Single(journal.Pending());Assert.NotEqual(WorkflowStrategyMachineStatus.Started,(await authority.ReadCurrentAsync(view.WorkflowId.Value,default))!.Status);
        }
        using(var journal=new WorkflowPublicationJournal(root))
        {
            await using var fixture=new WorkflowDispatchDeliveryFixture(publications:journal,publicationOwner:authority);fixture.Time.At=view.UpdatedAtUtc.AddHours(1);
            fixture.Send=_=>throw new InvalidOperationException("Already accepted output must not be re-executed after restart");
            await WorkflowPublicationDelivery.RecoverPendingAsync(fixture.Context);Assert.Empty(journal.Pending());Assert.Empty(fixture.Commands);
            var routed=source with{Subject=new(ActorType.Realtime,"IntrinsicTimeStrategyWorkflowRealtime",WorkflowStrategyStateUpdatedEvent.Verb,view.EntityId.Format())};
            // The event-log reader restores its storage EventId; test that exact historical shape.
            var recovered=await transactions.ExecuteOnceAsync(async(tx,ct)=>
            {
                var rows=await tx.QueryAsync("SELECT eventversion FROM event_log WHERE commandid=$1;",[source.CommandId],r=>r.GetInt64(0),ct);return rows.Single();
            });
            var hash=WorkflowRequestHash.Compute(routed with{EventId=recovered});
            var proven=await authority.ReadLegacySnapshotAsync(command.StreamId,hash,default);Assert.NotNull(proven);
            Assert.Equal(MessagePackSerializer.Serialize(view),MessagePackSerializer.Serialize(proven));
            Assert.Null(await authority.ReadLegacySnapshotAsync(command.StreamId,new string('F',64),default));
        }
        Directory.Delete(root,true);

        // An unaccepted result from a different calculation session may not be
        // promoted to a trusted result by deserializing it. The real owner stops it.
        var coldView=view with{WorkflowId=new(Guid.NewGuid()),CurrentStage=StrategyWorkflowStage.RiskManagement,DecisionRuntimeVersion=1,DecisionRuntimeSessionId=Guid.NewGuid(),
            RiskManagement=new(){StartedAtUtc=view.UpdatedAtUtc,ProcessingStatus=StrategyActorProcessingStatus.Processing}};
        var coldSource=source with{Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),WorkflowId=coldView.WorkflowId,State=coldView};
        var coldOutput=new CompleteRiskManagementCommand{CommandId=Guid.NewGuid(),EntityId=coldView.EntityId,WorkflowId=coldView.WorkflowId,EntryApproval=new(),
            InputWorkflowRevision=coldView.WorkflowRevision,SourceEventId=Guid.NewGuid(),CompletedAtUtc=coldView.UpdatedAtUtc,
            Subject=new(ActorType.Command,CompleteRiskManagementCommand.Actor,CompleteRiskManagementCommand.Verb,coldView.EntityId.Format())};
        // Distinct workflow on the same assignment stream, following the first two committed snapshots.
        await transactions.ExecuteOnceAsync(async(tx,ct)=>{await tx.AppendAsync(new(coldOutput.StreamId,coldSource.CommandId,coldSource,2),ct);await tx.WorkflowProjectAsync(coldSource,ct);return true;});
        var coldState=new IntrinsicTimeStrategyWorkflowCommandState();Assert.True(coldState.Apply(coldSource,addEvent:false));
        var coldRoot=Path.Combine(Path.GetTempPath(),"ifm-workflow-cold-pg-"+Guid.NewGuid().ToString("N"));
        using(var capture=new WorkflowPublicationJournal(coldRoot))capture.Prepare(WorkflowPublicationDelivery.Key(coldView),WorkflowPublicationDelivery.Digest(coldView),coldView,coldView.DecisionRuntimeSessionId,coldOutput);
        using(var journal=new WorkflowPublicationJournal(coldRoot))
        {
            await using var fixture=new WorkflowDispatchDeliveryFixture(publications:journal,publicationOwner:authority);fixture.Time.At=coldView.UpdatedAtUtc;
            async Task AcceptStopAsync(RedispatchCurrentStrategyPipelineCommand stop)
            {
                    Assert.True(stop.Execute(owner,coldState).Success);
                    await transactions.ExecuteOnceAsync(async(tx,ct)=>
                    {
                        await tx.ReserveCommandAsync(stop,ct);long revision=3;
                        foreach(var change in coldState.Events.OfType<WorkflowStrategyStateUpdatedEvent>())
                        {await tx.AppendAsync(new(stop.StreamId,stop.CommandId,change,revision++),ct);await tx.WorkflowProjectAsync(change,ct);}
                        return true;
                    });
            }
            fixture.Context.SendAsync<RedispatchCurrentStrategyPipelineCommand,IntrinsicTimeStrategyWorkflowEntityId>(Arg.Any<RedispatchCurrentStrategyPipelineCommand>(),Arg.Any<IntrinsicTimeStrategyWorkflowEntityId>())
                .Returns(call=>new ValueTask(AcceptStopAsync(call.Arg<RedispatchCurrentStrategyPipelineCommand>())));
            await WorkflowPublicationDelivery.RecoverPendingAsync(fixture.Context);Assert.Empty(journal.Pending());
            Assert.Equal("WF.DECISION.INTERRUPTED",coldState.CurrentView!.StopReasonCode);
            Assert.Null(coldState.CurrentView.EntryCommit);Assert.Empty(fixture.Commands);
        }
        Directory.Delete(coldRoot,true);

        // Recover the actual Fund reservation owner from PostgreSQL after its
        // original allocation committed but the callback was lost.
        var selection=await TradeSelectionHandoffTests.Fixture.Create();selection.Accept();
        var handoff=selection.State.CurrentView!.CompositionHandoff!;var inputs=selection.Command.SelectionBinding.PortfolioSnapshot;
        var allocations=new PortfolioFundCompositionAggregate();
        var original=allocations.Reserve(handoff.Request,inputs,7001,[8001],selection.Clock.Now,"workflow-test");
        var fund=new PortfolioFundAggregate();fund.RestoreSnapshot(new(1,inputs.Fund,[],[..allocations.CaptureState()],[]));
        var snapshotOptions=MessagePackSerializerOptions.Standard.WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
        var frozen=MessagePackSerializer.Serialize(fund.CaptureSnapshot(),snapshotOptions);
        await transactions.ExecuteOnceAsync(async(tx,ct)=>
        {
            await tx.ExecuteAsync("CREATE TABLE workflow_fund_recovery_snapshot(operationid uuid PRIMARY KEY,payload bytea NOT NULL);",[],ct);
            await tx.ExecuteAsync("INSERT INTO workflow_fund_recovery_snapshot VALUES($1,$2);",[handoff.Request.IdempotencyKey,frozen],ct);return true;
        });
        var stored=await transactions.ExecuteOnceAsync(async(tx,ct)=>(await tx.QueryAsync("SELECT payload FROM workflow_fund_recovery_snapshot WHERE operationid=$1;",[handoff.Request.IdempotencyKey],r=>r.GetFieldValue<byte[]>(0),ct)).Single());
        var restoredFund=new PortfolioFundAggregate();restoredFund.RestoreSnapshot(MessagePackSerializer.Deserialize<PortfolioFundAggregateSnapshot>(stored,snapshotOptions));
        var reconcile=new ReserveFundOrderCompositionCommand(handoff.Request,inputs){CommandId=Guid.NewGuid(),ReconcileOnly=true};
        Assert.True(reconcile.Execute(restoredFund,handoff.Request.ExpiresAtUtc.AddDays(1),"workflow-recovery").Success);
        Assert.Equal(frozen,MessagePackSerializer.Serialize(restoredFund.CaptureSnapshot(),snapshotOptions));
        Assert.True(restoredFund.TryComposition(handoff.Request.IdempotencyKey,out var receipt));Assert.Equal(original.Order.OrderId,receipt.Order.OrderId);
        Assert.Equal(original.Trades.Single().TradeId,receipt.Trades.Single().TradeId);Assert.Equal(original.CanonicalRequestSha256,receipt.CanonicalRequestSha256);
    }
    sealed class Clock(DateTime at):TimeProvider{public override DateTimeOffset GetUtcNow()=>new(at);}
}
