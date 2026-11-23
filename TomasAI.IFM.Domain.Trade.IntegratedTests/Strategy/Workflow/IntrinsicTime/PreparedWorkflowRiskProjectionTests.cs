using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Fact, Trait("Category", "PreparedWorkflowRuntime")]
    public async Task Ready_five_stage_evidence_projects_to_the_assigned_fund_and_preserves_the_large_approval_hash()
    {
        var view = MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(
            await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ready-futures-no-trade-view.msgpack")))!;
        view.EntryCommit!.Approval.HasValidFingerprint().Should().BeTrue();
        await using var host = Host(); _ = host.CreateClient();
        try
        {
            var context = (IIntrinsicTimeStrategyWorkflowCommandContext)host.Services.GetRequiredService<SimpleInjector.Container>()
                .GetInstance<ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor>>();
            var source = new WorkflowStrategyStateUpdatedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EventId = 771337,
                EntityId = view.EntityId, WorkflowId = view.WorkflowId, WorkflowRevision = view.WorkflowRevision,
                Subject = new(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor, WorkflowStrategyStateUpdatedEvent.Verb, view.EntityId.Format()),
                UpdatedAtUtc = view.UpdatedAtUtc, ReceivedOn = view.UpdatedAtUtc, State = view };
            var row = RiskHistoryIdentity.Row(source)!;
            row.FundId.Should().Be(view.EntityId.Assignment!.FundId);
            row.Outcome.Should().Be("NoTrade");
            var transactions = host.Services.GetRequiredService<IPostgresEventTransaction>();
            await transactions.ExecuteOnceAsync(async (db, token) =>
            {
                await ((IEnlistedTradeDbWriter)context.DbFactory.TradeDb).ProjectWorkflowAsync(db, source, token);
                return true;
            });
            var saved = await context.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowAsync(view.WorkflowId);
            var restored = MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(saved!.StatePayload)!;
            restored.EntryCommit!.Approval.HasValidFingerprint().Should().BeTrue();
            restored.EntryCommit.Approval.PayloadSha256.Should().Be(view.EntryCommit.Approval.PayloadSha256);
            var history = await transactions.ExecuteOnceAsync((db, token) => db.QueryAsync(
                "SELECT payload FROM trade.risk_management_history WHERE portfolio_id=$1 AND fund_id=$2 AND invocation_id=$3;",
                [row.PortfolioId, row.FundId, row.InvocationId], reader => reader.GetFieldValue<byte[]>(0), token));
            history.Should().ContainSingle();
            MessagePackBinarySerializer.Shared.Deserialize<RiskHistoryRow>(history[0]).Should().BeEquivalentTo(row);
        }
        finally { await global::SupervisorTestLifecycle.ShutdownAsync(host.Services); }
    }
}
