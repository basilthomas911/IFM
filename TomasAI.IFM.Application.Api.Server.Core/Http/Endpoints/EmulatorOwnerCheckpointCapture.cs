using System.Diagnostics;
using TomasAI.IFM.Application.TradeBroker.Mapping;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Domain.BrokerAccount.Realtime;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
namespace TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;

/// <summary>Owner-issued evidence only; this operation never submits an order or changes an economic field.</summary>
public static class EmulatorOwnerCheckpointCapture
{
    public sealed record Result(EmulatorOwnerCheckpointEvidence Owner,BrokerAccountDefinition PersistedAccount,string SnapshotSha256,
        bool FinancialValuesMatched,bool QualifiedSession=false);
    public static bool Matches(BrokerAccountSnapshotEvidence expected,BrokerAccountSnapshotEvidence actual)
        =>expected.AccountAlias==actual.AccountAlias&&expected.Currency==actual.Currency&&expected.CashBalance==actual.CashBalance&&expected.AvailableFunds==actual.AvailableFunds
        &&expected.Complete==actual.Complete&&expected.NewRiskAllowed==actual.NewRiskAllowed&&expected.Generation==actual.Generation&&expected.SourceSequence==actual.SourceSequence
        &&expected.Positions.SequenceEqual(actual.Positions);
    public static async Task<Result> CaptureAsync(EmulatorLedger ledger,BrokerAccountObservationBridge bridge,IBrokerAccountReadStore reads,Guid boundary,CancellationToken cancellationToken,DateTime? originalUtc=null,Func<ValueTask<DateTime>>? prepareOriginalRequest=null)
    {
        EmulatorOwnerCheckpointEvidence before=null!;BrokerAccountSnapshotEvidence expected=null!;
        await bridge.PublishOwnerCheckpointAsync(async _=>
        {
            if(prepareOriginalRequest is not null)originalUtc=await prepareOriginalRequest().ConfigureAwait(false);
            var receipt=ledger.CheckpointOwner(boundary,originalUtc);
            before=ledger.CaptureOwnerCheckpointEvidence(boundary);
            return expected=BrokerAccountSnapshotEvidence.From(TradeBrokerMapper.ToApplication(receipt.Snapshot));
        },cancellationToken).ConfigureAwait(false);
        before=ledger.CaptureOwnerCheckpointEvidence(boundary);
        var started=Stopwatch.GetTimestamp();
        while(Stopwatch.GetElapsedTime(started)<TimeSpan.FromSeconds(10))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current=await reads.GetAsync(new(expected.AccountAlias),cancellationToken).ConfigureAwait(false);
            if(current is {Snapshot:{ } snapshot}&&Matches(expected,snapshot))
            {
                var after=ledger.CaptureOwnerCheckpointEvidence(boundary);
                if(before.EffectivePath!=after.EffectivePath||before.Length!=after.Length||before.Sha256!=after.Sha256
                    ||before.Receipt.BoundaryId!=after.Receipt.BoundaryId||before.Receipt.CommitRevision!=after.Receipt.CommitRevision
                    ||before.Receipt.Snapshot.AsOfUtc!=after.Receipt.Snapshot.AsOfUtc
                    ||!Matches(BrokerAccountSnapshotEvidence.From(TradeBrokerMapper.ToApplication(before.Receipt.Snapshot)),BrokerAccountSnapshotEvidence.From(TradeBrokerMapper.ToApplication(after.Receipt.Snapshot))))
                    throw new InvalidOperationException("Owner changed during projection convergence.");
                return new(after,current,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePack.MessagePackSerializer.Serialize(current.Snapshot))),true);
            }
            await Task.Delay(50,cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Authentic broker-account projection did not converge to the owner commit; no source binding certified.");
    }
}
