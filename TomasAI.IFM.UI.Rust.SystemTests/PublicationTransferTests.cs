using MessagePack;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class PublicationTransferTests
{
    [Fact] public void Unchanged_bytes_do_not_allocate_or_advance_revision() {
        using var stream = new PublicationTransfer(incremental:true,trace:true);
        var bytes = BridgeDocument.Encode(new { value = new string('x', 1000) });
        var first = stream.Read(bytes, 0)!.Value;
        Assert.False(first.Patch);
        Assert.Null(stream.Read(bytes.ToArray(), first.Sequence));
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) Assert.Null(stream.Read(bytes, first.Sequence));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - allocated);
    }
    [Fact] public void Nested_quote_change_is_small_and_missed_revision_recovers_full() {
        using var stream = new PublicationTransfer(incremental:true,trace:true);
        byte[] Snapshot(double bid) => BridgeWire.Serialize(new ContractEditorPublication(1, 1, "Ready", true,
            new("options", Enumerable.Range(0, 100).Select(i => new ContractEditorRow(i.ToString(), [])
            { Data = BridgeDocument.Encode(new { bid = i == 50 ? bid : 1d, padding = new string('x', 100) }) }).ToArray(), []), []));
        var first = stream.Read(Snapshot(1), 0)!.Value;
        var second = stream.Read(Snapshot(2), first.Sequence)!.Value;
        Assert.True(second.Patch);
        Assert.True(second.Bytes.Length < first.Bytes.Length / 10);
        var patch = BridgeWire.Deserialize<TransferPatch>(second.Bytes);
        Assert.Contains(patch.Operations, op => op.Path.Any(p => p.Kind == 2));
        var thirdBytes = Snapshot(3);
        var missed = stream.Read(thirdBytes, first.Sequence)!.Value;
        Assert.False(missed.Patch);
        Assert.Same(thirdBytes, missed.Bytes);
    }
    [Fact] public void Newest_first_log_uses_prepend_and_trim() {
        using var stream = new PublicationTransfer(incremental:true,trace:true);
        byte[] Snapshot(int start) => BridgeDocument.Encode(new { rows = Enumerable.Range(start, 100).Select(i => new { id = i, text = new string('x', 80) }).ToArray() });
        var first = stream.Read(Snapshot(1), 0)!.Value;
        var next = stream.Read(Snapshot(0), first.Sequence)!.Value;
        Assert.True(next.Patch);
        var patch = BridgeWire.Deserialize<TransferPatch>(next.Bytes);
        Assert.Equal(2, patch.Operations.Length);
        Assert.All(patch.Operations, op => Assert.Equal(2, op.Action));
    }
    [Fact] public void Cross_language_fixtures_preserve_nested_binary_and_log_splices() {
        var directory=Environment.GetEnvironmentVariable("IFM_TRANSFER_FIXTURES");
        if(string.IsNullOrEmpty(directory))return;
        Directory.CreateDirectory(directory);
        byte[] Snapshot(int start,double bid) => BridgeDocument.Encode(new {
            rows=Enumerable.Range(start,100).Select(i=>new { id=i,text=new string('x',80) }).ToArray(),
            data=BridgeDocument.Encode(new { bid,padding=new string('x',1000),missing=(string?)null }),
            lastQuoteReceivedAtUtc="2026-10-16T12:00:00.0000000+00:00"
        });
        using var stream=new PublicationTransfer(incremental:true,trace:true);
        var first=stream.Read(Snapshot(1,1),0)!.Value;
        File.WriteAllBytes(Path.Combine(directory,"full.msgpack"),first.Bytes);
        var expected=Snapshot(0,2);
        var next=stream.Read(expected,first.Sequence)!.Value;
        Assert.True(next.Patch);
        Assert.True(next.SourceTicks>0);
        File.WriteAllBytes(Path.Combine(directory,"patch.msgpack"),next.Bytes);
        File.WriteAllBytes(Path.Combine(directory,"expected.msgpack"),expected);
    }
    [Fact]
    public void Cached_history_rows_preserve_patch_and_full_recovery_after_prepend_and_trim()
    {
        using var cache = new TradePlanRowCache();
        var plans = Enumerable.Range(1, 100).Select(i => TradePlanRowCacheTests.Plan(i)).Reverse().ToArray();
        var scope = new TradePlanHistoryScope(plans[0].Position.Id, plans[0].ValueDate, true);
        byte[] Encode(TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot[] window)
            => BridgeWire.Serialize(new ContractEditorPublication(1, 1, "Ready", false,
                new("trade-orders", cache.Rows(window, scope), []), []));
        using var stream = new PublicationTransfer(incremental: true);
        var first = stream.Read(Encode(plans), 0)!.Value;
        var secondPlans = new[] { TradePlanRowCacheTests.Plan(101) }.Concat(plans.Take(99)).ToArray();
        var expected = Encode(secondPlans);
        var next = stream.Read(expected, first.Sequence)!.Value;
        Assert.True(next.Patch); Assert.True(next.Bytes.Length < first.Bytes.Length);
        Assert.Equal(101, cache.Encodes);
        if (Environment.GetEnvironmentVariable("IFM_TRANSFER_FIXTURES") is { Length: > 0 } fixtures)
        {
            var directory = Path.Combine(fixtures, "trade-history");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "full.msgpack"), first.Bytes);
            File.WriteAllBytes(Path.Combine(directory, "patch.msgpack"), next.Bytes);
            File.WriteAllBytes(Path.Combine(directory, "expected.msgpack"), expected);
        }
        var thirdPlans = new[] { TradePlanRowCacheTests.Plan(102) }.Concat(secondPlans.Take(99)).ToArray();
        var bytes = Encode(thirdPlans);
        var missed = stream.Read(bytes, first.Sequence)!.Value;
        Assert.False(missed.Patch); Assert.Same(bytes, missed.Bytes);
        Assert.Null(stream.Read(bytes, missed.Sequence));
    }
}
