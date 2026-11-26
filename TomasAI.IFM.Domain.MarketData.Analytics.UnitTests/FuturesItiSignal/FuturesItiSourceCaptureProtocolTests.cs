using MessagePack;
using System.Security.Cryptography;
using NSubstitute;
using System.Reflection;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

[CollectionDefinition("ITI source capture protocol",DisableParallelization=true)]
public sealed class ItiSourceCaptureProtocolCollection;
[Collection("ITI source capture protocol")]
public sealed class FuturesItiSourceCaptureProtocolTests:IDisposable
{
    readonly bool previousHealth=TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.FuturesItiOrderedIngress.AllowsEntries;
    readonly string previousReason=TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.FuturesItiOrderedIngress.StatusReason;
    public void Dispose()=>TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.FuturesItiOrderedIngress.SetHealth(previousHealth,previousReason);
    static string Root()=>Path.Combine(Path.GetTempPath(),"ifm-source-protocol-"+Guid.NewGuid().ToString("N"));
    static GenerateFuturesItiSignalCommand Input()=>new("ES20261218",new DateOnly(2026,11,20),TimeFrameType.Daily,new DateTime(2026,11,20,15,0,0,DateTimeKind.Utc),6510,19.5)
    {CommandId=Guid.NewGuid(),InputsFrozen=true,RequireDailyAtr=true,SourceCaptureRequired=true,SourceEventId=Guid.NewGuid(),SourceCursor=new("GLBX.MDP3",1,42,Guid.NewGuid(),1,912,new DateTime(2026,11,20,15,0,0,DateTimeKind.Utc),new DateTime(2026,11,20,15,0,1,DateTimeKind.Utc))};
    static byte[] LegacyEnvelope(byte[] current)
    {
        var reader=new MessagePackReader(current);var fields=reader.ReadArrayHeader();
        var output=new System.Buffers.ArrayBufferWriter<byte>();var writer=new MessagePackWriter(output);writer.WriteArrayHeader(fields);
        var commandFields=reader.ReadArrayHeader();Assert.Equal(21,commandFields);writer.WriteArrayHeader(commandFields-2);
        for(var field=0;field<commandFields;field++){var raw=reader.ReadRaw();if(field<commandFields-2)writer.WriteRaw(raw);}
        for(var field=1;field<fields;field++)writer.WriteRaw(reader.ReadRaw());writer.Flush();return output.WrittenSpan.ToArray();
    }
    [Fact]
    public async Task Prepared_only_source_survives_cold_WAL_without_becoming_released()
    {
        var root=Root();var input=Input();var bytes=FuturesItiJournalEnvelope.Encode(input);
        try
        {
            await using(var wal=new FuturesItiInputWal(root)){await wal.AppendAsync(bytes,default);Assert.False(wal.IsSourceReleased(input));}
            await using(var wal=new FuturesItiInputWal(root))
            {
                var rows=new List<FuturesItiInputWal.Record>();await foreach(var row in wal.ReplayAsync())rows.Add(row);
                var restored=FuturesItiJournalEnvelope.Decode(Assert.Single(rows).Payload,0);
                Assert.Equal(input.SourceEventId,restored.SourceEventId);Assert.Equal(input.SourceCursor,restored.SourceCursor);Assert.True(restored.InputsFrozen);
                Assert.Equal(input.VixFuturesPrice,restored.VixFuturesPrice);Assert.Equal(input.Timestamp,restored.Timestamp);Assert.False(wal.IsSourceReleased(restored));
                wal.ReleaseSource(restored);Assert.True(wal.IsSourceReleased(restored));
            }
            await using(var wal=new FuturesItiInputWal(root)){Assert.True(wal.IsSourceReleased(input));var changed=input with{FuturesPrice=input.FuturesPrice+1};Assert.Throws<InvalidDataException>(()=>wal.IsSourceReleased(changed));}
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Legacy_commands_keep_their_original_bytes_and_do_not_require_new_release_markers()
    {
        var root=Root();var legacy=Input() with{SourceCaptureRequired=false,SourceEventId=Guid.Empty};var bytes=LegacyEnvelope(FuturesItiJournalEnvelope.Encode(legacy));
        try{await using var wal=new FuturesItiInputWal(root);var restored=FuturesItiJournalEnvelope.Decode(bytes,4);Assert.False(restored.SourceCaptureRequired);Assert.True(wal.IsSourceReleased(restored));Assert.Equal(bytes,restored.JournalPayload);}
        finally{Directory.Delete(root,true);}
    }
    static IFuturesItiSignalRealtimeContext Context(out IMarketDataApi market)
        {
            var context=NSubstitute.Substitute.For<IFuturesItiSignalRealtimeContext>();market=NSubstitute.Substitute.For<IMarketDataApi>();
            context.MarketDataApi.Returns(market);context.Telemetry.Returns(new FuturesItiSignalRuntimeTelemetry(TimeProvider.System));context.HealthEvidence.Returns(new LivePipelineEvidence(TimeProvider.System));return context;
        }
    static FuturesItiOrderedRuntime Runtime(IFuturesItiSignalRealtimeContext context,FuturesItiInputWal wal,string root)
            =>(FuturesItiOrderedRuntime)Activator.CreateInstance(typeof(FuturesItiOrderedRuntime),BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{context,wal,root},null)!;
    static async ValueTask Invoke(FuturesItiOrderedRuntime runtime,string method,FuturesMarketPriceUpdatedRealtimeEvent source)
            =>await (ValueTask)typeof(FuturesItiOrderedRuntime).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(runtime,new object[]{source,CancellationToken.None})!;

    static FuturesMarketPriceUpdatedRealtimeEvent[] BatchSources(int count)
    {
        var date=new DateOnly(2026,11,20);var time=new DateTimeOffset(2026,11,20,15,0,0,TimeSpan.Zero);var epoch=Guid.NewGuid();var generation=Guid.NewGuid();
        return Enumerable.Range(1,count).Select(index=>new FuturesMarketPriceUpdatedRealtimeEvent{Id=Guid.NewGuid(),EntityId=new("ES20261218",date,AssetTypeId.Futures),SourceDataset="GLBX.MDP3",SourceGenerationId=generation,UpdateSource=FuturesMarketPriceUpdateSource.Trade,
            Price=new("ES20261218",42,1,AssetTypeId.Futures,date,null,new FuturesMarketTradeSnapshot(6510+index*.25m,2,912+index,time.AddMilliseconds(index),time.AddMilliseconds(index+1),NormalizedTradeAction.New,NormalizedTradeSide.Buy,NormalizedTradeConditionFlags.None,epoch,index))}).ToArray();
    }
    [Fact]
    public async Task Batch_preparation_freezes_all_original_inputs_and_retry_never_resamples_them()
    {
        var root=Root();var sources=BatchSources(64);
        try
        {
            await using var wal=new FuturesItiInputWal(root);var context=Context(out var market);await using var runtime=Runtime(context,wal,root);
            await runtime.PrepareBatchAsync(sources,default);market.ClearReceivedCalls();
            await runtime.PrepareBatchAsync(sources,default);Assert.Empty(market.ReceivedCalls());
            var records=new List<FuturesItiInputWal.Record>();await foreach(var row in wal.ReplayAsync())records.Add(row);
            Assert.Equal(64,records.Count);Assert.Equal(sources.Select(source=>source.Id),records.Select(row=>FuturesItiJournalEnvelope.Decode(row.Payload,0).SourceEventId));
            foreach(var row in records)Assert.False(wal.IsSourceReleased(FuturesItiJournalEnvelope.Decode(row.Payload,0)));
            await Assert.ThrowsAsync<InvalidDataException>(()=>runtime.PrepareBatchAsync([sources[0] with{SourceDataset="changed"}],default).AsTask());
            Assert.Empty(market.ReceivedCalls());
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Reversed_confirmed_batch_is_not_released_or_allowed_to_overtake()
    {
        var root=Root();var sources=BatchSources(2);
        try
        {
            await using var wal=new FuturesItiInputWal(root);await using var runtime=Runtime(Context(out _),wal,root);
            await runtime.PrepareBatchAsync(sources,default);
            await Assert.ThrowsAsync<InvalidDataException>(()=>runtime.DeliverBatchAsync(sources.Reverse().ToArray(),default).AsTask());
            await foreach(var row in wal.ReplayAsync())Assert.False(wal.IsSourceReleased(FuturesItiJournalEnvelope.Decode(row.Payload,0)));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Grouped_WAL_and_release_proofs_keep_legacy_frames_order_and_safe_prefix_compaction()
    {
        var root=Root();var inputs=Enumerable.Range(0,64).Select(index=>Input() with{FuturesPrice=6500+index}).ToArray();
        var payloads=inputs.Select(FuturesItiJournalEnvelope.Encode).ToArray();
        try
        {
            await using(var wal=new FuturesItiInputWal(root,1024))
            {
                var positions=await wal.AppendBatchAsync(payloads,default);Assert.Equal(64,positions.Length);
                Assert.Equal(positions.OrderBy(position=>position.Segment).ThenBy(position=>position.EndOffset),positions);
                wal.ReleaseSourcesBatch(inputs);foreach(var input in inputs)Assert.True(wal.IsSourceReleased(input));
                var before=new List<byte[]>();await foreach(var row in wal.ReplayAsync())before.Add(row.Payload);Assert.Equal(payloads.Length,before.Count);
                for(var index=0;index<payloads.Length;index++)Assert.Equal(payloads[index],before[index]);
                await wal.AcknowledgeAsync(positions[31]);
                var remaining=new List<GenerateFuturesItiSignalCommand>();await foreach(var row in wal.ReplayAsync())remaining.Add(FuturesItiJournalEnvelope.Decode(row.Payload,0));
                foreach(var input in remaining)Assert.True(wal.IsSourceReleased(input));
                Assert.False(wal.IsSourceReleased(inputs[0])); // Deleted prefix no longer pins release receipts forever.
            }
            await using(var wal=new FuturesItiInputWal(root,1024))
                await foreach(var row in wal.ReplayAsync())Assert.True(wal.IsSourceReleased(FuturesItiJournalEnvelope.Decode(row.Payload,0)));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Batch_validation_and_cancellation_leave_no_partially_accepted_WAL_group()
    {
        var root=Root();
        try
        {
            await using var wal=new FuturesItiInputWal(root);
            await Assert.ThrowsAsync<InvalidDataException>(()=>wal.AppendBatchAsync([new byte[]{1},new byte[61*1024]],default).AsTask());
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>wal.AppendBatchAsync([new byte[]{1}],cancelled.Token).AsTask());
            var records=new List<FuturesItiInputWal.Record>();await foreach(var row in wal.ReplayAsync())records.Add(row);Assert.Empty(records);
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Batch_cold_recovery_preserves_every_original_causal_command_and_unreleased_boundary()
    {
        var root=Root();var sources=BatchSources(64);var expected=new List<byte[]>();
        try
        {
            await using(var wal=new FuturesItiInputWal(root))
            {
                await using var runtime=Runtime(Context(out _),wal,root);await runtime.PrepareBatchAsync(sources,default);
                await foreach(var row in wal.ReplayAsync())expected.Add(row.Payload);
            }
            await using(var wal=new FuturesItiInputWal(root))
            {
                var context=Context(out var market);await using var runtime=Runtime(context,wal,root);
                foreach(var source in sources)await Invoke(runtime,"ValidateOriginalTradeAsync",source);
                Assert.Empty(market.ReceivedCalls());var index=0;
                await foreach(var row in wal.ReplayAsync())
                {Assert.Equal(expected[index++],row.Payload);Assert.False(wal.IsSourceReleased(FuturesItiJournalEnvelope.Decode(row.Payload,0)));}
                Assert.Equal(64,index);
            }
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void Grouped_release_rejects_a_changed_member_before_writing_any_new_proof()
    {
        var root=Root();var journal=Guid.NewGuid();var existing=Guid.NewGuid();var newSource=Guid.NewGuid();
        try
        {
            using(var receipts=new FuturesItiRecoveryCoverage(root,journal))
            {
                receipts.Record(existing,new byte[]{1});
                Assert.Throws<InvalidDataException>(()=>receipts.RecordBatch([(newSource,new byte[]{2}),(existing,new byte[]{9})]));
                Assert.False(receipts.Contains(newSource,new byte[]{2}));Assert.Equal(1,receipts.Count);
                receipts.RecordBatch([(newSource,new byte[]{2}),(existing,new byte[]{1})]);Assert.Equal(2,receipts.Count);
            }
            using var restored=new FuturesItiRecoveryCoverage(root,journal);Assert.True(restored.Contains(newSource,new byte[]{2}));Assert.True(restored.Contains(existing,new byte[]{1}));
        }
        finally{Directory.Delete(root,true);}
    }

    [Fact]
    public async Task Actual_ordered_intake_preparation_does_not_release_and_cold_validation_never_resamples_causal_inputs()
    {
        var root=Root();var date=new DateOnly(2026,11,20);var time=new DateTimeOffset(2026,11,20,15,0,0,TimeSpan.Zero);var epoch=Guid.NewGuid();
        var source=new FuturesMarketPriceUpdatedRealtimeEvent{Id=Guid.NewGuid(),EntityId=new("ES20261218",date,AssetTypeId.Futures),SourceDataset="GLBX.MDP3",SourceGenerationId=Guid.NewGuid(),UpdateSource=FuturesMarketPriceUpdateSource.Trade,
            Price=new("ES20261218",42,1,AssetTypeId.Futures,date,null,new FuturesMarketTradeSnapshot(6510,2,912,time,time.AddMilliseconds(1),NormalizedTradeAction.New,NormalizedTradeSide.Buy,NormalizedTradeConditionFlags.None,epoch,1))};
        try
        {
            await using(var wal=new FuturesItiInputWal(root))
            {
                await using var runtime=Runtime(Context(out _),wal,root);
                await Invoke(runtime,"PrepareOriginalTradeAsync",source);
                Assert.False(runtime.DrainStatus.Drained);
                Assert.Equal(1,runtime.DrainStatus.PreparedSources);
                await foreach(var row in wal.ReplayAsync()){var frozen=FuturesItiJournalEnvelope.Decode(row.Payload,0);Assert.True(frozen.SourceCaptureRequired);Assert.Equal(source.Id,frozen.SourceEventId);Assert.True(frozen.InputsFrozen);Assert.False(wal.IsSourceReleased(frozen));}
            }
            await using(var wal=new FuturesItiInputWal(root))
            {
                var context=Context(out var market);await using var runtime=Runtime(context,wal,root);
                await Invoke(runtime,"ValidateOriginalTradeAsync",source);
                Assert.Empty(market.ReceivedCalls());
                await foreach(var row in wal.ReplayAsync())Assert.False(wal.IsSourceReleased(FuturesItiJournalEnvelope.Decode(row.Payload,0)));
            }
        }
        finally{Directory.Delete(root,true);}
    }

    [Fact]
    public async Task Native_only_original_source_records_explicit_unavailable_context_and_sticky_gap_across_restart()
    {
        var root=Root();var date=new DateOnly(2026,11,20);var time=new DateTimeOffset(2026,11,20,15,0,0,TimeSpan.Zero);var epoch=Guid.NewGuid();
        var source=new FuturesMarketPriceUpdatedRealtimeEvent{Id=Guid.NewGuid(),EntityId=new("ES20261218",date,AssetTypeId.Futures),SourceDataset="GLBX.MDP3",SourceGenerationId=Guid.NewGuid(),UpdateSource=FuturesMarketPriceUpdateSource.Trade,
            Price=new("ES20261218",42,1,AssetTypeId.Futures,date,null,new FuturesMarketTradeSnapshot(6510,2,912,time,time.AddMilliseconds(1),NormalizedTradeAction.New,NormalizedTradeSide.Buy,NormalizedTradeConditionFlags.None,epoch,1))};
        try
        {
            await using(var wal=new FuturesItiInputWal(root))
            {
                await using var runtime=Runtime(Context(out var market),wal,root);
                await Invoke(runtime,"ValidateOriginalTradeAsync",source);Assert.Empty(market.ReceivedCalls());
                Assert.Equal(1,runtime.Snapshot.GetType().GetProperty("HistoricalCausalGaps")!.GetValue(runtime.Snapshot));
                Assert.True(runtime.DrainStatus.Failed);
                Assert.False(runtime.DrainStatus.Drained);
                await foreach(var row in wal.ReplayAsync())
                {
                    var frozen=FuturesItiJournalEnvelope.Decode(row.Payload,0);Assert.Contains("Historical source",frozen.InputUnavailableReason);Assert.Null(frozen.DailyAtr);Assert.Equal(0,frozen.VixFuturesPrice);Assert.Equal(time.UtcDateTime,frozen.Timestamp);Assert.Equal(6510,frozen.FuturesPrice);Assert.False(wal.IsSourceReleased(frozen));
                }
            }
            await using(var wal=new FuturesItiInputWal(root))
            {
                await using var runtime=Runtime(Context(out var market),wal,root);
                Assert.Equal(1,runtime.Snapshot.GetType().GetProperty("HistoricalCausalGaps")!.GetValue(runtime.Snapshot));
                await Invoke(runtime,"ValidateOriginalTradeAsync",source);Assert.Empty(market.ReceivedCalls());
                Assert.Equal(1,runtime.Snapshot.GetType().GetProperty("HistoricalCausalGaps")!.GetValue(runtime.Snapshot));
            }
        }
        finally{Directory.Delete(root,true);}
    }

    [Fact]
    public void Exact_replay_receipts_survive_restart_and_refuse_changed_source_bytes_and_partial_tail()
    {
        var root=Root();var journal=Guid.NewGuid();var source=Guid.NewGuid();byte[] body=[1,2,3];
        try
        {
            using(var receipts=new FuturesItiRecoveryCoverage(root,journal)){Assert.False(receipts.Contains(source,body));receipts.Record(source,body);}
            using(var receipts=new FuturesItiRecoveryCoverage(root,journal)){Assert.True(receipts.Contains(source,body));Assert.Throws<InvalidDataException>(()=>receipts.Contains(source,new byte[]{1,2,4}));}
            var path=Directory.GetFiles(root).Single();using(var file=new FileStream(path,FileMode.Append)){file.WriteByte(1);file.Flush(true);}
            Assert.Throws<InvalidDataException>(()=>new FuturesItiRecoveryCoverage(root,journal));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact]
    public void Partial_replay_authority_restores_original_progress_and_rejects_corruption()
    {
        var root=Root();var journal=Guid.NewGuid();var epoch=Guid.NewGuid();var source=Guid.NewGuid();
        try
        {
            var authority=new FuturesItiReplayAuthority(root,journal);authority.Save(new([new("ES20261218",Guid.NewGuid(),3,epoch,source,SHA256.HashData(new byte[]{1}))],[epoch]));
            var restored=new FuturesItiReplayAuthority(root,journal).Restore();Assert.Equal(source,Assert.Single(restored.Progress).SourceId);Assert.Equal(3,restored.Progress[0].Batch);Assert.Equal(epoch,Assert.Single(restored.VerifiedEpochs));
            var path=Directory.GetFiles(root).Single();var bytes=File.ReadAllBytes(path);bytes[^1]^=1;File.WriteAllBytes(path,bytes);Assert.Throws<InvalidDataException>(()=>authority.Restore());
        }
        finally{Directory.Delete(root,true);}
    }
}
