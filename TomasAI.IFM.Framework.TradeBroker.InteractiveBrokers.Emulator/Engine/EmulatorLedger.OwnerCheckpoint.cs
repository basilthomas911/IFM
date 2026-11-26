using TomasAI.IFM.Framework.TradeBroker.Contracts;
namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

public sealed record EmulatorOwnerCheckpointRequest(Guid BoundaryId,DateTime RequestedAtUtc);
public sealed record EmulatorOwnerCheckpointReceipt(Guid BoundaryId,long CommitRevision,FrameworkAccountSnapshot Snapshot);
public sealed record EmulatorOwnerCheckpointEvidence(EmulatorOwnerCheckpointReceipt Receipt,string EffectivePath,long Length,string Sha256);
public sealed record EmulatorLedgerFileEvidence(string EffectivePath,long Length,string Sha256);
public interface IEmulatorLedgerFileEvidence
{
    string EffectivePath {get;}
    EmulatorOwnerCheckpointRequest FreezeCheckpointRequest(EmulatorOwnerCheckpointRequest proposed,bool exact);
    EmulatorLedgerFileEvidence CaptureEvidence(EmulatorLedgerCheckpoint expected);
}
public sealed record EmulatorOwnerCheckpointStatus(string AccountAlias,string? EffectivePath,long CommitRevision,long Generation,long Sequence,bool PendingCommit,int ReceiptCount);
public sealed partial class EmulatorLedger
{
    readonly Dictionary<Guid,EmulatorOwnerCheckpointReceipt> _ownerCheckpoints=[];
    static EmulatorOwnerCheckpointReceipt CopyOwnerReceipt(EmulatorOwnerCheckpointReceipt value)=>value with{Snapshot=value.Snapshot with{Positions=[..value.Snapshot.Positions]}};
    public EmulatorOwnerCheckpointStatus OwnerCheckpointStatus(){lock(_sync)return new(AccountAlias,(_store as IEmulatorLedgerFileEvidence)?.EffectivePath,_committed.CommitRevision,_committed.Generation,_committed.Sequence,_pendingCommit is not null,_ownerCheckpoints.Count);}
    public bool HasPendingCommit {get{lock(_sync)return _pendingCommit is not null;}}
    /// <summary>Durably identifies the existing owner state without creating a broker operation or economic fact.</summary>
    public EmulatorOwnerCheckpointRequest FreezeOwnerCheckpointRequest(Guid boundaryId,DateTime? originalUtc=null)
    {
        if(boundaryId==Guid.Empty||originalUtc is{} time&&time.Kind!=DateTimeKind.Utc)throw new ArgumentException("An identified UTC owner checkpoint is required.");
        lock(_sync)
        {
            if(_pendingCommit?.Candidate.OwnerCheckpoints?.SingleOrDefault(x=>x.BoundaryId==boundaryId) is{} pending)
            {if(originalUtc is{} priorTime&&priorTime!=pending.Snapshot.AsOfUtc)throw new InvalidOperationException("Owner checkpoint request time conflicts.");return new(boundaryId,pending.Snapshot.AsOfUtc);}
            if(_ownerCheckpoints.TryGetValue(boundaryId,out var prior)){if(originalUtc is{} exact&&exact!=prior.Snapshot.AsOfUtc)throw new InvalidOperationException("Owner checkpoint request time conflicts.");return new(boundaryId,prior.Snapshot.AsOfUtc);}
            var request=new EmulatorOwnerCheckpointRequest(boundaryId,originalUtc??_clock.UtcNow);
            return _store is IEmulatorLedgerFileEvidence file?file.FreezeCheckpointRequest(request,originalUtc is not null):request;
        }
    }
    public EmulatorOwnerCheckpointReceipt CheckpointOwner(Guid boundaryId,DateTime? originalUtc=null)
    {
        if(boundaryId==Guid.Empty)throw new ArgumentException("A nonempty checkpoint boundary is required.",nameof(boundaryId));
        lock(_sync)
        {
            var request=FreezeOwnerCheckpointRequest(boundaryId,originalUtc);
            var receipt=Transact(()=>
            {
                if(_ownerCheckpoints.TryGetValue(boundaryId,out var prior))return prior;
                if(_ownerCheckpoints.Count>=4096)throw new InvalidOperationException("Owner checkpoint receipt capacity reached; retain existing evidence.");
                var next=checked(_committed.CommitRevision+1);
                var value=new EmulatorOwnerCheckpointReceipt(boundaryId,next,Snapshot() with{SourceSequence=next,AsOfUtc=request.RequestedAtUtc});
                _ownerCheckpoints.Add(boundaryId,value);Persist();return value;
            });
            _accountEvents.Writer.TryWrite(true);
            return CopyOwnerReceipt(receipt);
        }
    }
    /// <summary>Returns proof from the effective single-writer store only while this exact checkpoint remains current.</summary>
    public EmulatorOwnerCheckpointEvidence CaptureOwnerCheckpointEvidence(Guid boundaryId)
    {
        lock(_sync)
        {
            RequireCommitted();
            if(!_ownerCheckpoints.TryGetValue(boundaryId,out var receipt)||receipt.CommitRevision!=_committed.CommitRevision)
                throw new InvalidOperationException("Owner checkpoint is absent or superseded; a common boundary cannot be certified.");
            if(_store is not IEmulatorLedgerFileEvidence file)throw new InvalidOperationException("The actual owner is not backed by a verifiable file store.");
            var evidence=file.CaptureEvidence(_committed);
            return new(CopyOwnerReceipt(receipt),evidence.EffectivePath,evidence.Length,evidence.Sha256);
        }
    }
}
