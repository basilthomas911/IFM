using System.Text.Json;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using Xunit;
namespace TomasAI.IFM.Application.TradeBroker.UnitTests;
public sealed class EmulatorOwnerCheckpointTests
{
    static EmulatorScenario Scenario()=>new("EMU-CHECKPOINT","USD",12345.67m,0m,TimeSpan.FromSeconds(30),1);
    sealed class Clock:IEmulatorClock{public DateTime UtcNow{get;set;}=new(2026,11,26,13,0,0,DateTimeKind.Utc);}
    sealed class Store:IEmulatorLedgerStore
    {
        public EmulatorLedgerCheckpoint? Value;public int Saves;public bool FailBefore,FailAfter;
        public EmulatorLedgerCheckpoint? Load()=>Value;
        public void Save(EmulatorLedgerCheckpoint checkpoint){Saves++;if(FailBefore){FailBefore=false;throw new IOException("before");}Value=checkpoint;if(FailAfter){FailAfter=false;throw new IOException("after");}}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void Uncertain_checkpoint_retries_original_commit_and_does_not_create_economic_operations(bool after)
    {
        var store=new Store();var clock=new Clock();var ledger=new EmulatorLedger(Scenario(),clock,store);var id=Guid.NewGuid();
        if(after)store.FailAfter=true;else store.FailBefore=true;
        Assert.Throws<IOException>(()=>ledger.CheckpointOwner(id));Assert.True(ledger.HasPendingCommit);Assert.False(ledger.Snapshot().Complete);
        if(after){var cold=new EmulatorLedger(Scenario(),clock,store);var count=store.Saves;Assert.Equal(1,cold.CheckpointOwner(id).CommitRevision);Assert.Equal(count,store.Saves);}
        clock.UtcNow=clock.UtcNow.AddHours(1);var receipt=ledger.CheckpointOwner(id);Assert.Equal(1,receipt.CommitRevision);Assert.Equal(13,receipt.Snapshot.AsOfUtc.Hour);
        Assert.False(ledger.HasPendingCommit);var saved=store.Load()!;Assert.Equal(12345.67m,saved.Cash);Assert.Equal(0,saved.Sequence);Assert.Equal(1,saved.Generation);
        Assert.Empty(saved.Orders);Assert.Empty(saved.Operations);Assert.Empty(saved.Positions);Assert.Empty(saved.Journal);Assert.Empty(saved.Observations);
        var writes=store.Saves;Assert.Equal(JsonSerializer.Serialize(receipt),JsonSerializer.Serialize(ledger.CheckpointOwner(id)));Assert.Equal(writes,store.Saves);
        var restart=new EmulatorLedger(Scenario(),clock,store);Assert.Equal(JsonSerializer.Serialize(receipt),JsonSerializer.Serialize(restart.CheckpointOwner(id)));Assert.Equal(writes,store.Saves);
    }
    [Fact]
    public void Original_request_freezes_before_archive_or_commit_and_survives_cold_retry_without_restamping()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-owner-request-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var path=Path.Combine(root,"ledger.json");var id=Guid.NewGuid();var clock=new Clock();EmulatorOwnerCheckpointRequest original;
        try
        {
            using(var store=new FileEmulatorLedgerStore(path)){var ledger=new EmulatorLedger(Scenario(),clock,store);original=ledger.FreezeOwnerCheckpointRequest(id);Assert.False(File.Exists(path));}
            clock.UtcNow=clock.UtcNow.AddHours(2);
            using(var store=new FileEmulatorLedgerStore(path))
            {
                var ledger=new EmulatorLedger(Scenario(),clock,store);Assert.Equal(original,ledger.FreezeOwnerCheckpointRequest(id));
                Assert.Throws<InvalidDataException>(()=>ledger.FreezeOwnerCheckpointRequest(id,clock.UtcNow));
                var receipt=ledger.CheckpointOwner(id,original.RequestedAtUtc);Assert.Equal(original.RequestedAtUtc,receipt.Snapshot.AsOfUtc);Assert.Equal(1,receipt.CommitRevision);
            }
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void Receipt_capacity_refuses_only_new_boundaries_without_poisoning_existing_owner_proof()
    {
        var store=new Store();var ledger=new EmulatorLedger(Scenario(),new Clock(),store);var first=Guid.NewGuid();ledger.CheckpointOwner(first);
        for(var i=1;i<4096;i++)ledger.CheckpointOwner(Guid.NewGuid());
        var before=store.Saves;Assert.Throws<InvalidOperationException>(()=>ledger.CheckpointOwner(Guid.NewGuid()));
        Assert.False(ledger.HasPendingCommit);Assert.Equal(before,store.Saves);Assert.Equal(1,ledger.CheckpointOwner(first).CommitRevision);Assert.Equal(before,store.Saves);
        var cold=new EmulatorLedger(Scenario(),new Clock(),store);Assert.Equal(4096,cold.OwnerCheckpointStatus().ReceiptCount);Assert.Equal(1,cold.CheckpointOwner(first).CommitRevision);
    }
    [Fact]
    public void Legacy_file_is_upgraded_by_real_owner_commit_preserving_all_original_fields_and_actual_path_proof()
    {
        var root=Path.Combine(Path.GetTempPath(),"ifm-owner-checkpoint-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var path=Path.Combine(root,"actual.json");
        try
        {
            var originalTime=new DateTime(2026,1,2,12,0,0,DateTimeKind.Utc);
            var legacy=new EmulatorLedgerCheckpoint(12345.67m,224,33,[],[],[new("ES-ORIGINAL",2,4500.2500m)],[new(224,"Original","original-order",Guid.NewGuid(),12345.67m,originalTime)],[],
                LatestQuotes:[new("ES-ORIGINAL",4500.25m,4500.50m,3,4,originalTime,7,99)]);
            File.WriteAllText(path,JsonSerializer.Serialize(legacy));var id=Guid.NewGuid();EmulatorOwnerCheckpointReceipt receipt;
            using(var store=new FileEmulatorLedgerStore(path))
            {
                var ledger=new EmulatorLedger(Scenario(),new Clock(),store);receipt=ledger.CheckpointOwner(id);var actual=store.Load()!;
                Assert.Equal(legacy.Cash,actual.Cash);Assert.Equal(224,actual.Sequence);Assert.Equal(33,actual.Generation);Assert.Equal(1,actual.CommitRevision);
                var normalizedLegacy=legacy with{QuoteReceipts=[],MatchReceipts=[]};
                Assert.Equal(JsonSerializer.Serialize(normalizedLegacy),JsonSerializer.Serialize(actual with{CommitRevision=0,OwnerCheckpoints=null}));
                receipt.Snapshot.Positions[0]=new("CORRUPTED-CALLER",99,1m);
                Assert.Equal("ES-ORIGINAL",ledger.CheckpointOwner(id).Snapshot.Positions[0].ContractId);
                receipt=ledger.CheckpointOwner(id);
                var proof=ledger.CaptureOwnerCheckpointEvidence(id);Assert.Equal(Path.GetFullPath(path),proof.EffectivePath);Assert.Equal(new FileInfo(path).Length,proof.Length);
                Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))),proof.Sha256);
                ledger.CheckpointOwner(Guid.NewGuid());Assert.Throws<InvalidOperationException>(()=>ledger.CaptureOwnerCheckpointEvidence(id));
            }
            using(var reopened=new FileEmulatorLedgerStore(path)){var ledger=new EmulatorLedger(Scenario(),new Clock(),reopened);Assert.Equal(JsonSerializer.Serialize(receipt),JsonSerializer.Serialize(ledger.CheckpointOwner(id)));}
        }
        finally{Directory.Delete(root,true);}
    }
}
