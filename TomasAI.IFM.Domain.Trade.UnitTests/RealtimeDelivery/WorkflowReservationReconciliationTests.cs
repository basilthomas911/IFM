using System.Buffers;
using MessagePack;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Fund.Command;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class WorkflowReservationReconciliationTests
{
    [Fact]
    public async Task Reconcile_only_on_real_fund_never_allocates_and_only_expires_after_original_validity()
    {
        var f=await TradeSelectionHandoffTests.Fixture.Create();f.Accept();var pending=f.State.CurrentView!.CompositionHandoff!;
        var owner=new PortfolioFundAggregate();
        var command=new ReserveFundOrderCompositionCommand(pending.Request,f.Command.SelectionBinding.PortfolioSnapshot){CommandId=Guid.NewGuid(),ReconcileOnly=true};
        var before=command.Execute(owner,pending.Request.ExpiresAtUtc.AddTicks(-1),"workflow-recovery");
        Assert.False(before.Success);Assert.Equal(PortfolioErrorCodes.ReservationNotYetSettled,before.ErrorCode);Assert.Empty(owner.Orders);Assert.Equal(0,owner.Revision);
        var expired=command.Execute(owner,pending.Request.ExpiresAtUtc,"workflow-recovery");
        Assert.False(expired.Success);Assert.Equal(PortfolioErrorCodes.ReservationExpiredWithoutEffects,expired.ErrorCode);Assert.Empty(owner.Orders);Assert.Equal(0,owner.Revision);
        // Reconciliation does not consume the original operation identity or a prepared order/trade allocation.
        Assert.Null(command.Preparation);Assert.False(owner.TryComposition(pending.Request.IdempotencyKey,out _));
    }

    [Fact]
    public async Task Newly_appended_null_reconciliation_field_preserves_legacy_command_retry_encoding()
    {
        var f=await TradeSelectionHandoffTests.Fixture.Create();f.Accept();var pending=f.State.CurrentView!.CompositionHandoff!;
        var command=new ReserveFundOrderCompositionCommand(pending.Request,f.Command.SelectionBinding.PortfolioSnapshot){CommandId=Guid.NewGuid()};
        var codec=new CommandAuditMessagePackCodec();var current=codec.Serialize(command);
        var legacy=WithoutAppendedNull(current.Bytes);
        var restored=(ReserveFundOrderCompositionCommand)codec.Deserialize(typeof(ReserveFundOrderCompositionCommand),legacy,(short)current.Format,current.Version);
        Assert.Null(restored.ReconcileOnly);Assert.True(codec.MatchesStoredEncoding(restored,legacy));
        Assert.Equal(codec.Serialize(((ICommandRetryIdentity)command).ForRetryIdentity()).Sha256,codec.Serialize(((ICommandRetryIdentity)restored).ForRetryIdentity()).Sha256);
        var reconcile=command with{ReconcileOnly=true};Assert.False(codec.MatchesStoredEncoding(reconcile,legacy));
    }

    static byte[] WithoutAppendedNull(byte[] bytes)
    {
        var input=new MessagePackReader(bytes);var count=input.ReadArrayHeader();Assert.Equal(13,count);
        var buffer=new ArrayBufferWriter<byte>();var output=new MessagePackWriter(buffer);output.WriteArrayHeader(count-1);
        for(var index=0;index<count-1;index++)output.WriteRaw(input.ReadRaw());
        Assert.True(input.TryReadNil());Assert.True(input.End);output.Flush();return buffer.WrittenSpan.ToArray();
    }
}
