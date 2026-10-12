using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public sealed record FeedDrainDiagnostics
{
    public required FeedDrainStage Stage { get; init; }
    public required long NativeReadCallCount { get; init; }
    public required uint LastNativeReadRecordCount { get; init; }
    public required ulong LastNativeReadFirstSequence { get; init; }
    public required ulong LastNativeReadLastSequence { get; init; }
    public required uint LastNativeReadRecordsRouted { get; init; }
    public required int CurrentNativeReadRecordIndex { get; init; }
    public required string CurrentRecordKind { get; init; }
    public required ushort CurrentPublisherId { get; init; }
    public required uint CurrentInstrumentId { get; init; }
    public required uint CurrentSourceSequence { get; init; }
    public required bool ManagedBatchPublishActive { get; init; }
    public required int ManagedBatchPublishRecordCount { get; init; }
    public required ushort ManagedBatchPublisherId { get; init; }
    public required uint ManagedBatchInstrumentId { get; init; }
}
