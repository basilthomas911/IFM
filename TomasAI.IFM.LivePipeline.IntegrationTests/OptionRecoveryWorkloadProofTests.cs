using MessagePack;
using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.OptionChainCache;
using TomasAI.IFM.Application.MarketData.Pricing;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class OptionRecoveryWorkloadProofTests
{
    [MessagePackObject]
    public sealed record OldReadSnapshot([property:Key(0)] MarketDataBackendStatus Status,
        [property:Key(1)] TomasAI.IFM.Application.MarketData.Databento.Workers.DatasetSubscriptionManifest[] Manifests,
        [property:Key(2)] TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[] Contracts,
        [property:Key(3)] TomasAI.IFM.Application.MarketData.Databento.Workers.DatasetPublicationEnvelope[] CurrentValues,
        [property:Key(4)] OptionChainPublication[]? OptionChains=null,
        [property:Key(5)] MarketDataBootstrapPrice[]? BootstrapPrices=null,
        [property:Key(6)] TomasAI.IFM.Application.MarketData.Databento.Workers.DatasetPublicationEnvelope[]? OptionObservations=null);

    [Fact]
    public void Workload_evidence_is_additive_in_both_MessagePack_reader_directions()
    {
        var status=new MarketDataBackendStatus(true,new(2026,10,27),[]);
        var old=new OldReadSnapshot(status,[],[],[]);
        var modern=MessagePackSerializer.Deserialize<MarketDataReadSnapshot>(MessagePackSerializer.Serialize(old));
        Assert.Null(modern.NativeOptionWorkload);
        var proof=new NativeOptionWorkloadEvidence(Guid.NewGuid(),DateTimeOffset.UtcNow,false,[],[],[]);
        var reread=MessagePackSerializer.Deserialize<OldReadSnapshot>(MessagePackSerializer.Serialize(modern with{NativeOptionWorkload=proof}));
        Assert.Equal(status.ValueDate,reread.Status.ValueDate);Assert.Empty(reread.CurrentValues);
    }

    [MessagePackObject]
    public sealed record OriginalWorkload([property:Key(0)]Guid GenerationId,[property:Key(1)]DateTimeOffset ObservedAtUtc,
        [property:Key(2)]bool ReplacementPending,[property:Key(3)]string[] Scopes,[property:Key(4)]string[] ReadyScopes,[property:Key(5)]string[] OptionScopes);
    [Fact]
    public void Native_resource_proof_fields_are_optional_and_compatible_in_both_wire_directions()
    {
        var original=new OriginalWorkload(Guid.NewGuid(),DateTimeOffset.UnixEpoch,false,["scope"],["scope"],["scope"]);
        var modern=MessagePackSerializer.Deserialize<NativeOptionWorkloadEvidence>(MessagePackSerializer.Serialize(original));
        Assert.Equal(0,modern.FormatVersion);Assert.False(modern.NativeResourceProofsComplete);
        var restored=MessagePackSerializer.Deserialize<OriginalWorkload>(MessagePackSerializer.Serialize(modern with{FormatVersion=1,NativeResourceProofsComplete=true}));
        Assert.Equal(original.GenerationId,restored.GenerationId);Assert.Equal(original.Scopes,restored.Scopes);
    }

    [Theory]
    [InlineData(true,1,true)]
    [InlineData(false,1,false)]
    [InlineData(true,2,false)]
    [InlineData(true,0,false)]
    public void Native_resource_proof_accepts_background_without_inventing_candidates(bool complete,int version,bool expected)
    {
        var now=new DateTimeOffset(2026,10,27,14,0,0,TimeSpan.Zero);var date=new DateOnly(2026,10,27);
        var dataset=new MarketDataDatasetStatus("Options","GLBX.MDP3",Guid.NewGuid(),Guid.NewGuid(),123,true,true,"Healthy",null,date);
        var status=new MarketDataBackendStatus(true,date,[dataset],Guid.NewGuid(),EnabledOptionTrades:[],RecoveryOwnership:MarketDataRecoveryOwnership.TokioDatasetOwner);
        var evidence=new NativeOptionWorkloadEvidence(dataset.GenerationId,now,false,["background"],["background"],["background"],version,complete);
        var snapshot=new MarketDataReadSnapshot(status,[],[],[],NativeOptionWorkload:evidence);
        Assert.Equal(expected,OptionRecoveryWorkloadProof.IsComplete(snapshot,dataset,now.AddSeconds(-5),now));
        Assert.False(OptionRecoveryWorkloadProof.IsComplete(snapshot with{Status=status with{EnabledOptionTrades=["live-trade"]}},dataset,now.AddSeconds(-5),now));
        Assert.False(OptionRecoveryWorkloadProof.IsComplete(snapshot with{Status=status with{EnabledOptionTrades=null}},dataset,now.AddSeconds(-5),now));
        Assert.False(OptionRecoveryWorkloadProof.IsComplete(snapshot with{NativeOptionWorkload=evidence with{ReadyScopes=[]}},dataset,now.AddSeconds(-5),now));
        Assert.False(OptionRecoveryWorkloadProof.IsComplete(snapshot with{NativeOptionWorkload=evidence with{GenerationId=Guid.NewGuid()}},dataset,now.AddSeconds(-5),now));
    }

    [Theory]
    [InlineData("idle", true)]
    [InlineData("background", true)]
    [InlineData("underlying", true)]
    [InlineData("missing-evidence", false)]
    [InlineData("replacing", false)]
    [InlineData("not-ready", false)]
    [InlineData("stale-generation", false)]
    [InlineData("stale-observation", false)]
    [InlineData("future-observation", false)]
    [InlineData("missing-publication", false)]
    [InlineData("unused-fallback", true)]
    [InlineData("old-publication", false)]
    [InlineData("expired-publication", false)]
    [InlineData("wrong-publication-date", false)]
    [InlineData("wrong-publication-generation", false)]
    [InlineData("empty-publication", false)]
    [InlineData("unready-underlying", false)]
    [InlineData("hidden-scope", false)]
    [InlineData("owner-changed", false)]
    [InlineData("process", false)]
    [InlineData("enabled-trade", false)]
    [InlineData("unknown-trades", false)]
    [InlineData("failed", false)]
    [InlineData("unhealthy", false)]
    public void Native_workload_requires_complete_current_evidence(string mode, bool accepted)
    {
        var now = new DateTimeOffset(2026,10,27,14,0,0,TimeSpan.Zero);
        var started = now.AddSeconds(-10);var date = new DateOnly(2026,10,27);var generation=Guid.NewGuid();
        var dataset = new MarketDataDatasetStatus("Options","GLBX.MDP3",Guid.NewGuid(),generation,123,true,true,"Healthy",null,date);
        var scopes = mode == "idle" ? Array.Empty<string>() : mode == "underlying" ? ["underlying-ES"] : new[]{"scope-one","scope-two","underlying-ES"};
        var evidence = new NativeOptionWorkloadEvidence(generation,now,false,scopes,scopes,scopes.Where(x=>!x.StartsWith("underlying-",StringComparison.Ordinal)).ToArray());
        var publications = evidence.OptionScopes.Select(scope=>new OptionChainPublication(
            new MarketCompositionSnapshot(1,Guid.NewGuid(),scope,"token","Daily",generation,now,now.AddMinutes(1),[new(null!,null)],"digest"),date,"config",1,scope)).ToArray();
        var status = new MarketDataBackendStatus(true,date,[dataset],Guid.NewGuid(),EnabledOptionTrades:[],RecoveryOwnership:MarketDataRecoveryOwnership.TokioDatasetOwner);
        switch(mode)
        {
            case "missing-evidence": evidence=null;break;
            case "replacing": evidence=evidence with{ReplacementPending=true};break;
            case "not-ready": evidence=evidence with{ReadyScopes=[]};break;
            case "stale-generation": evidence=evidence with{GenerationId=Guid.NewGuid()};break;
            case "stale-observation": evidence=evidence with{ObservedAtUtc=started.AddTicks(-1)};break;
            case "future-observation": evidence=evidence with{ObservedAtUtc=now.AddTicks(1)};break;
            case "missing-publication": publications=[];break;
            case "unused-fallback": publications=publications.Take(1).ToArray();break;
            case "old-publication": publications=publications.Select(x=>x with{Snapshot=x.Snapshot with{EvaluatedAtUtc=started.AddTicks(-1)}}).ToArray();break;
            case "expired-publication": publications=publications.Select(x=>x with{Snapshot=x.Snapshot with{ValidUntilUtc=now}}).ToArray();break;
            case "wrong-publication-date": publications=publications.Select(x=>x with{ValueDate=date.AddDays(-1)}).ToArray();break;
            case "wrong-publication-generation": publications=publications.Select(x=>x with{Snapshot=x.Snapshot with{GenerationId=Guid.NewGuid()}}).ToArray();break;
            case "empty-publication": publications=publications.Select(x=>x with{Snapshot=x.Snapshot with{Instruments=[]}}).ToArray();break;
            case "unready-underlying": evidence=evidence with{ReadyScopes=scopes.Where(x=>!x.StartsWith("underlying-",StringComparison.Ordinal)).ToArray()};break;
            case "hidden-scope": evidence=evidence with{OptionScopes=[]};break;
            case "owner-changed": status=status with{Datasets=[dataset with{WorkerInstanceId=Guid.NewGuid()}]};break;
            case "process": status=status with{RecoveryOwnership=MarketDataRecoveryOwnership.Process};break;
            case "enabled-trade": status=status with{EnabledOptionTrades=["1901:1501"]};break;
            case "unknown-trades": status=status with{EnabledOptionTrades=null};break;
            case "failed": status=status with{Ready=false};break;
            case "unhealthy": status=status with{Datasets=[dataset with{Healthy=false}]};break;
        }
        var snapshot=new MarketDataReadSnapshot(status,[],[],[],publications,NativeOptionWorkload:evidence);
        // Exercise the actual MessagePack transport before proof, not only CLR objects.
        snapshot=MessagePackSerializer.Deserialize<MarketDataReadSnapshot>(MessagePackSerializer.Serialize(snapshot));
        Assert.Equal(accepted,OptionRecoveryWorkloadProof.IsComplete(snapshot,dataset,started,now));
    }
    [Theory]
    [InlineData(0,true)]
    [InlineData(64,true)]
    [InlineData(128,true)]
    [InlineData(192,true)]
    [InlineData(256,true)]
    [InlineData(257,false)]
    public void Native_resource_scope_capacity_preserves_all_four_bounded_owner_groups(int count,bool accepted)
    {
        var now=new DateTimeOffset(2026,10,27,14,0,0,TimeSpan.Zero);var date=new DateOnly(2026,10,27);
        var dataset=new MarketDataDatasetStatus("Options","GLBX.MDP3",Guid.NewGuid(),Guid.NewGuid(),123,true,true,"Healthy",null,date);
        var status=new MarketDataBackendStatus(true,date,[dataset],Guid.NewGuid(),EnabledOptionTrades:[],RecoveryOwnership:MarketDataRecoveryOwnership.TokioDatasetOwner);
        var scopes=Enumerable.Range(0,count).Select(i=>$"scope-{i}").ToArray();
        var evidence=new NativeOptionWorkloadEvidence(dataset.GenerationId,now,false,scopes,scopes,scopes,1,true);
        var snapshot=MessagePackSerializer.Deserialize<MarketDataReadSnapshot>(MessagePackSerializer.Serialize(new MarketDataReadSnapshot(status,[],[],[],NativeOptionWorkload:evidence)));
        Assert.Equal(accepted,OptionRecoveryWorkloadProof.IsComplete(snapshot,dataset,now.AddSeconds(-5),now));
        if(count>0) {
            Assert.False(OptionRecoveryWorkloadProof.IsComplete(snapshot with{NativeOptionWorkload=evidence with{ReadyScopes=scopes.Skip(1).ToArray()}},dataset,now.AddSeconds(-5),now));
            Assert.False(OptionRecoveryWorkloadProof.IsComplete(snapshot with{NativeOptionWorkload=evidence with{Scopes=scopes.Append(scopes[0]).ToArray()}},dataset,now.AddSeconds(-5),now));
        }
    }

}
