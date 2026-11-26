using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class WorkflowPublicationWireCompatibilityTests
{
    [Theory]
    [InlineData(64)]
    [InlineData(200_000)]
    public void Output_and_frozen_input_bytes_match_the_original_unoptimized_writer(int size)
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-workflow-wire-"+Guid.NewGuid().ToString("N"));
        var original=WorkflowDispatchDeliveryFixture.RegimeFunctionInput(representative:true);
        var intent=original.State with{StopReasonCode=new string('X',size)};
        var key=WorkflowPublicationDelivery.Key(intent);var digest=WorkflowPublicationDelivery.Digest(intent);var session=Guid.NewGuid();
        var command=new FailRegimeDiscoveryCommand{CommandId=Guid.NewGuid(),EntityId=intent.EntityId,WorkflowId=intent.WorkflowId,
            InputWorkflowRevision=intent.WorkflowRevision,SourceEventId=original.Id,FailedAtUtc=intent.UpdatedAtUtc};
        var clock=new WorkflowDispatchDeliveryFixture.Clock{At=intent.UpdatedAtUtc};
        var origin=MessageOrigins.ForPublication(command,clock,"wire-compat",Guid.NewGuid(),Guid.NewGuid(),original.Id);
        var request=new ReserveFundOrderCompositionRequest{WorkflowId=intent.WorkflowId.Value,WorkflowRevision=intent.WorkflowRevision,IdempotencyKey=Guid.NewGuid(),
            RequestedAtUtc=intent.UpdatedAtUtc,ExpiresAtUtc=intent.ExpiresAtUtc,TradeSelectionResultSha256=new string('A',64)};
        MessageOrigins.Restore(request,origin);
        try
        {
            using(var journal=new WorkflowPublicationJournal(root))
            {
                var captured=journal.Prepare(key,digest,intent,session,command);
                Assert.Same(command,captured.Command);
                Assert.Equal(OriginalOutput(captured),File.ReadAllBytes(Path.Combine(root,"pending",key.ToString("N")+".wop")));
                journal.FreezeInput(key,digest,()=>request);
                Assert.Equal(OriginalInput(request,digest),File.ReadAllBytes(Path.Combine(root,"inputs",key.ToString("N")+".wip")));
                journal.Confirm(key,WorkflowPublicationDisposition.Accepted);
                Assert.Equal(OriginalOutput(captured with{Disposition=WorkflowPublicationDisposition.Accepted}),File.ReadAllBytes(Path.Combine(root,"done",key.ToString("N")+".wop")));
            }
            using(var cold=new WorkflowPublicationJournal(root))
            {
                var restored=cold.Find(key)!;
                Assert.Equal(WorkflowPublicationDisposition.Accepted,restored.Disposition);
                Assert.Equal(MessagePackSerializer.Serialize(intent),MessagePackSerializer.Serialize(restored.Intent));
                Assert.Equal(MessagePackSerializer.Serialize(command),MessagePackSerializer.Serialize((FailRegimeDiscoveryCommand)restored.Command));
                var input=cold.FreezeInput<ReserveFundOrderCompositionRequest>(key,digest,()=>throw new InvalidOperationException("Must restore exact original request."));
                Assert.Equal(MessagePackSerializer.Serialize(request),MessagePackSerializer.Serialize(input));
                Assert.Equal(origin,MessageOrigins.Get(input));
            }
        }
        finally{Directory.Delete(root,true);}
    }
    // Deliberately retained original allocation-heavy writer as byte oracle.
    static byte[] OriginalOutput(WorkflowPublication item)
    {
        using var payload=new MemoryStream();using(var writer=new BinaryWriter(payload,System.Text.Encoding.UTF8,true))
        {
            writer.Write(1);writer.Write(item.Key.ToByteArray());writer.Write(item.IntentHash);writer.Write(item.Session.ToByteArray());writer.Write((byte)item.Disposition);
            var view=MessagePackSerializer.Serialize(item.Intent);writer.Write(view.Length);writer.Write(view);
            writer.Write(item.Command.GetType().Name);var data=MessagePackSerializer.Serialize(item.Command.GetType(),item.Command);writer.Write(data.Length);writer.Write(data);
            var origin=MessageOrigins.Get(item.Command);writer.Write(origin is not null);if(origin is not null)writer.Write(MessageOriginBinary.Serialize(origin));
        }
        return OriginalFrame(payload);
    }
    static byte[] OriginalInput<T>(T value,string digest)
    {
        using var payload=new MemoryStream();using(var writer=new BinaryWriter(payload,System.Text.Encoding.UTF8,true))
        {
            writer.Write(2);writer.Write(typeof(T).FullName!);writer.Write(digest);var data=MessagePackSerializer.Serialize(value);writer.Write(data.Length);writer.Write(data);
            var origin=MessageOrigins.Get(value!);writer.Write(origin is not null);if(origin is not null)writer.Write(MessageOriginBinary.Serialize(origin));
        }
        return OriginalFrame(payload);
    }
    static byte[] OriginalFrame(MemoryStream payload)
    {var data=payload.ToArray();var bytes=new byte[data.Length+32];SHA256.HashData(data,bytes.AsSpan(0,32));data.CopyTo(bytes,32);return bytes;}
}
