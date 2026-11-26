using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class WorkflowPublicationRetentionTests
{
    [Fact]
    public void Saturation_refuses_new_sources_but_reserved_owned_confirmations_and_replay_still_finish()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-workflow-retention-"+Guid.NewGuid().ToString("N"));
        const long cap=3L*1024*1024;
        var seed=WorkflowDispatchDeliveryFixture.RegimeFunctionInput(representative:true);
        var view=seed.State with{StopReasonCode=new string('X',512*1024)};
        var digest=WorkflowPublicationDelivery.Digest(view);
        var first=Guid.NewGuid();var second=Guid.NewGuid();var rejected=Guid.NewGuid();
        FailRegimeDiscoveryCommand Command()=>new(){CommandId=Guid.NewGuid(),EntityId=view.EntityId,WorkflowId=view.WorkflowId,
            InputWorkflowRevision=view.WorkflowRevision,SourceEventId=seed.Id,FailedAtUtc=view.UpdatedAtUtc};
        var originals=new Dictionary<Guid,byte[]>();var inputs=new List<Guid>();
        try
        {
            using(var journal=new WorkflowPublicationJournal(root,cap))
            {
                foreach(var key in new[]{first,second})
                {
                    var command=Command(); originals.Add(key,MessagePackSerializer.Serialize(command));
                    var capture=journal.Prepare(key,digest,view,Guid.Empty,command);Assert.Same(command,capture.Command);
                    Assert.True(journal.RetainedBytes+journal.ReservedCompletionBytes<=cap);
                }
                Assert.Throws<IOException>(()=>journal.Prepare(rejected,digest,view,Guid.Empty,Command()));
                Assert.Null(journal.Find(rejected));Assert.Equal(2,journal.PendingCount);
                var saturated=false;
                for(var i=0;i<32;i++)
                {
                    var key=Guid.NewGuid();
                    try{journal.FreezeInput(key,digest,()=>view);inputs.Add(key);}
                    catch(IOException){saturated=true;break;}
                }
                Assert.True(saturated);Assert.NotEmpty(inputs);
                Assert.True(journal.RetainedBytes+journal.ReservedCompletionBytes<=cap);
                var proofs=Directory.EnumerateFiles(root,"*.wop",SearchOption.AllDirectories).Count();
                journal.Confirm(first,WorkflowPublicationDisposition.Accepted);journal.Confirm(second,WorkflowPublicationDisposition.Accepted);
                Assert.Equal(0,journal.PendingCount);Assert.Equal(0,journal.ReservedCompletionBytes);
                Assert.Equal(proofs,Directory.EnumerateFiles(root,"*.wop",SearchOption.AllDirectories).Count());
                Assert.True(journal.RetainedBytes<=cap);
            }
            using(var cold=new WorkflowPublicationJournal(root,cap))
            {
                foreach(var pair in originals)
                {
                    var saved=cold.Find(pair.Key)!;Assert.Equal(WorkflowPublicationDisposition.Accepted,saved.Disposition);
                    Assert.Equal(pair.Value,MessagePackSerializer.Serialize((FailRegimeDiscoveryCommand)saved.Command));Assert.Equal(digest,saved.IntentHash);
                }
                foreach(var key in inputs)
                    Assert.Equal(MessagePackSerializer.Serialize(view),MessagePackSerializer.Serialize(cold.FreezeInput<Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView>(key,digest,()=>throw new InvalidOperationException("Original input must survive."))));
                var actual=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Sum(path=>new FileInfo(path).Length);
                Assert.Equal(actual,cold.RetainedBytes);
            }
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void Recovery_over_reduced_capacity_fails_closed_without_removing_any_original_file()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-workflow-overcap-"+Guid.NewGuid().ToString("N"));
        var source=WorkflowDispatchDeliveryFixture.RegimeFunctionInput();var key=WorkflowPublicationDelivery.Key(source.State);
        try
        {
            using(var journal=new WorkflowPublicationJournal(root))journal.Prepare(key,WorkflowPublicationDelivery.Digest(source.State),source.State,Guid.Empty,
                new FailRegimeDiscoveryCommand{CommandId=Guid.NewGuid(),EntityId=source.EntityId,WorkflowId=source.WorkflowId,SourceEventId=source.Id,InputWorkflowRevision=source.WorkflowRevision,FailedAtUtc=source.UpdatedAtUtc});
            var path=Directory.EnumerateFiles(Path.Combine(root,"pending"),"*.wop").Single();var original=File.ReadAllBytes(path);
            Assert.Throws<IOException>(()=>new WorkflowPublicationJournal(root,original.Length)); // Body plus its guaranteed exact completion reserve no longer fits.
            Assert.Equal(original,File.ReadAllBytes(path));
            using var recovered=new WorkflowPublicationJournal(root);Assert.Equal(1,recovered.PendingCount);Assert.Equal(WorkflowPublicationDisposition.Pending,recovered.Find(key)!.Disposition);
        }
        finally{Directory.Delete(root,true);}
    }
}
