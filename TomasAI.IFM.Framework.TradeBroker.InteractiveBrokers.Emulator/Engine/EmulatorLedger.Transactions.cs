using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

public sealed partial class EmulatorLedger
{
    readonly record struct QuoteKey(string Contract, long Epoch, long Sequence, long MarketTicks);
    readonly Dictionary<QuoteKey, EmulatorQuoteReceipt> _quoteReceipts = [];
    readonly Dictionary<string, EmulatorMatchReceipt> _matchReceipts = new(StringComparer.Ordinal);
    readonly List<FrameworkBrokerObservation> _pendingFacts = [];
    EmulatorLedgerCheckpoint _committed = null!;
    PendingCommit? _pendingCommit;
    long _commitRevision;
    int _transactionDepth;
    bool _dirty;
    sealed record PendingCommit(EmulatorLedgerCheckpoint Before, EmulatorLedgerCheckpoint Candidate, FrameworkBrokerObservation[] Facts);

    // All callers hold _sync. A financial operation and every nested match have one checkpoint boundary.
    T Transact<T>(Func<T> action)
    {
        if (_transactionDepth != 0) return action();
        RecoverPendingCommit();
        var before = _committed;
        T result;
        FrameworkBrokerObservation[] facts;
        _transactionDepth = 1; _dirty = false; _pendingFacts.Clear();
        try
        {
            result = action();
            facts = [.. _pendingFacts];
            if (_dirty)
            {
                var candidate = CreateCheckpoint() with { CommitRevision = checked(before.CommitRevision + 1) };
                try { _store.Save(candidate); }
                catch
                {
                    // A returned error can mean either no write or a committed write with a lost reply.
                    // Freeze the exact calculated outcome, hide it from readers, and resolve on the next mutation.
                    _pendingCommit = new(before, candidate, facts);
                    throw;
                }
                _committed = candidate; _commitRevision = candidate.CommitRevision;
            }
        }
        catch { RestoreCheckpoint(before); throw; }
        finally { _transactionDepth = 0; _dirty = false; _pendingFacts.Clear(); }
        // Publication is deliberately outside the financial rollback region. Committed facts remain reconcilable.
        PublishFacts(facts);
        return result;
    }

    void RecoverPendingCommit()
    {
        if (_pendingCommit is not { } pending) return;
        var stored = _store.Load() ?? EmptyCheckpoint();
        if (!SameCheckpoint(stored, pending.Before) && !SameCheckpoint(stored, pending.Candidate))
            throw new InvalidDataException("Emulator checkpoint changed outside the pending financial commit.");
        // Re-saving the identical candidate confirms durable completion, including an uncertain prior rename.
        // One attempt per call, with no recomputation of prices, timestamps, fees, IDs or partial-fill quantity.
        _store.Save(pending.Candidate);
        RestoreCheckpoint(pending.Candidate); _committed = pending.Candidate; _pendingCommit = null;
        PublishFacts(pending.Facts);
    }
    void RequireCommitted()
    {
        if (_pendingCommit is not null) throw new IOException("Emulator financial checkpoint is unresolved; retry the retained operation before reading execution state.");
    }
    EmulatorLedgerCheckpoint EmptyCheckpoint() => new(_scenario.StartingCash,0,1,[],[],[],[],[],0,[],[],[]);
    static EmulatorLedgerCheckpoint Normalize(EmulatorLedgerCheckpoint value) => value with
    { LatestQuotes=value.LatestQuotes??[], QuoteReceipts=value.QuoteReceipts??[], MatchReceipts=value.MatchReceipts??[] };
    static bool SameCheckpoint(EmulatorLedgerCheckpoint left, EmulatorLedgerCheckpoint right)
        => left.CommitRevision==right.CommitRevision && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Normalize(left))),
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Normalize(right))));
    static QuoteKey QuoteIdentity(EmulatorQuote quote) => new(quote.ContractId,quote.SourceEpoch,quote.SourceSequence,quote.MarketTimeUtc.Ticks);
    static string MatchIdentity(string order,int revision,IReadOnlyList<EmulatorQuote> quotes)
    {
        using var buffer=new MemoryStream();
        using(var writer=new BinaryWriter(buffer,System.Text.Encoding.UTF8,true))
        {
            writer.Write(order);writer.Write(revision);writer.Write(quotes.Count);
            foreach(var quote in quotes){writer.Write(quote.ContractId);writer.Write(quote.SourceEpoch);writer.Write(quote.SourceSequence);writer.Write(quote.MarketTimeUtc.Ticks);}
        }
        return Convert.ToHexString(SHA256.HashData(buffer.GetBuffer().AsSpan(0,(int)buffer.Length)));
    }
    void RestoreCheckpoint(EmulatorLedgerCheckpoint checkpoint)
    {
        if(checkpoint.CommitRevision<0||checkpoint.Sequence<0||checkpoint.Generation<1)throw new InvalidDataException("Invalid emulator checkpoint revision.");
        _cash=checkpoint.Cash;_sequence=checkpoint.Sequence;_generation=checkpoint.Generation;_commitRevision=checkpoint.CommitRevision;
        if (checkpoint.PublishedOrderObservationCount < 0 || checkpoint.PublishedOrderObservationCount > checkpoint.Observations.Length)
            throw new InvalidDataException("Invalid emulator observation publication cursor.");
        _publishedOrderObservationCount = checkpoint.PublishedOrderObservationCount;
        _observationIndexes.Clear();
        for (var i = 0; i < checkpoint.Observations.Length; i++)
            _observationIndexes.Add(checkpoint.Observations[i].ObservationId, i);
        if(checkpoint.OwnerCheckpoints is{Length:>4096})throw new InvalidDataException("Owner checkpoint receipt capacity exceeded.");
        _ownerCheckpoints.Clear();
        foreach(var receipt in checkpoint.OwnerCheckpoints??[])
        {
            if(receipt.BoundaryId==Guid.Empty||receipt.CommitRevision<=0||receipt.CommitRevision>checkpoint.CommitRevision||receipt.Snapshot.SourceSequence!=receipt.CommitRevision||receipt.Snapshot.Generation<1||receipt.Snapshot.Generation>checkpoint.Generation||receipt.Snapshot.AsOfUtc.Kind!=DateTimeKind.Utc||receipt.Snapshot.AccountAlias!=AccountAlias||receipt.Snapshot.Currency!=_scenario.Currency||receipt.Snapshot.Positions is null||!receipt.Snapshot.Complete||!_ownerCheckpoints.TryAdd(receipt.BoundaryId,receipt))throw new InvalidDataException("Invalid owner checkpoint receipt.");
        }
        _orders.Clear();_operations.Clear();_positions.Clear();_journal.Clear();_observations.Clear();_latestQuotes.Clear();_quoteReceipts.Clear();_matchReceipts.Clear();
        foreach(var item in checkpoint.Orders)
        {
            if(item.Request.Legs.Length==0)throw new InvalidDataException("Invalid persisted emulator order.");
            var total=Math.Abs(item.Request.Legs[0].SignedQuantity);
            var filled=item.FilledStrategyUnits>0?item.FilledStrategyUnits:item.Filled?total:0;
            if(filled<0||filled>total)throw new InvalidDataException("Invalid persisted emulator fill quantity.");
            _orders.Add(item.Request.BrokerOrderId,new OrderState(item.Request){Limit=item.Limit,Revision=item.Revision,Filled=item.Filled,Cancelled=item.Cancelled,FilledStrategyUnits=filled});
        }
        foreach(var item in checkpoint.Operations)_operations.Add(item.OperationId,(item.Hash,item.Receipt));
        foreach(var item in checkpoint.Positions)_positions.Add(item.ContractId,item);
        _journal.AddRange(checkpoint.Journal);_observations.AddRange(checkpoint.Observations);
        foreach(var quote in checkpoint.LatestQuotes??[])_latestQuotes.Add(quote.ContractId,quote);
        foreach(var receipt in checkpoint.QuoteReceipts??[])_quoteReceipts.Add(QuoteIdentity(receipt.Quote),receipt);
        foreach(var receipt in checkpoint.MatchReceipts??[])
        {
            if(receipt.Key!=MatchIdentity(receipt.BrokerOrderId,receipt.Revision,receipt.Quotes)||receipt.FilledUnits<=0)throw new InvalidDataException("Invalid persisted emulator match receipt.");
            _matchReceipts.Add(receipt.Key,receipt);
        }
    }
}
