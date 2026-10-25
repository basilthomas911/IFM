using TomasAI.IFM.Shared.MarketData.Diagnostics;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

[MessagePackObject]
public sealed record DatasetWorkerDrainDiagnostics(
    [property: Key(0)] FeedDrainStage Stage,
    [property: Key(1)] long NativeReadCallCount,
    [property: Key(2)] uint LastNativeReadRecordCount,
    [property: Key(3)] ulong LastNativeReadFirstSequence,
    [property: Key(4)] ulong LastNativeReadLastSequence,
    [property: Key(5)] uint LastNativeReadRecordsRouted,
    [property: Key(6)] int CurrentNativeReadRecordIndex,
    [property: Key(7)] string CurrentRecordKind,
    [property: Key(8)] ushort CurrentPublisherId,
    [property: Key(9)] uint CurrentInstrumentId,
    [property: Key(10)] uint CurrentSourceSequence,
    [property: Key(11)] bool ManagedBatchPublishActive,
    [property: Key(12)] int ManagedBatchPublishRecordCount,
    [property: Key(13)] ushort ManagedBatchPublisherId,
    [property: Key(14)] uint ManagedBatchInstrumentId)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Stage) || CurrentRecordKind is null || CurrentRecordKind.Length > 64)
            throw new InvalidDataException("Worker drain diagnostic bounds are invalid.");
    }
    public static DatasetWorkerDrainDiagnostics From(FeedDrainDiagnostics value) 
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Stage,
        value.NativeReadCallCount, value.LastNativeReadRecordCount, value.LastNativeReadFirstSequence,
        value.LastNativeReadLastSequence, value.LastNativeReadRecordsRouted, value.CurrentNativeReadRecordIndex,
        DatasetWorkerDiagnostics.Bound(value.CurrentRecordKind, 64), value.CurrentPublisherId,
        value.CurrentInstrumentId, value.CurrentSourceSequence, value.ManagedBatchPublishActive,
        value.ManagedBatchPublishRecordCount, value.ManagedBatchPublisherId, value.ManagedBatchInstrumentId);
    }
    public FeedDrainDiagnostics ToDiagnostics() => new()
    {
        Stage = Stage,
        NativeReadCallCount = NativeReadCallCount,
        LastNativeReadRecordCount = LastNativeReadRecordCount,
        LastNativeReadFirstSequence = LastNativeReadFirstSequence,
        LastNativeReadLastSequence = LastNativeReadLastSequence,
        LastNativeReadRecordsRouted = LastNativeReadRecordsRouted,
        CurrentNativeReadRecordIndex = CurrentNativeReadRecordIndex,
        CurrentRecordKind = CurrentRecordKind,
        CurrentPublisherId = CurrentPublisherId,
        CurrentInstrumentId = CurrentInstrumentId,
        CurrentSourceSequence = CurrentSourceSequence,
        ManagedBatchPublishActive = ManagedBatchPublishActive,
        ManagedBatchPublishRecordCount = ManagedBatchPublishRecordCount,
        ManagedBatchPublisherId = ManagedBatchPublisherId,
        ManagedBatchInstrumentId = ManagedBatchInstrumentId
    };
}
