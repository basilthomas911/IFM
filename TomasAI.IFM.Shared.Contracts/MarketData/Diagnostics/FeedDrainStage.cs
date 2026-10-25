using TomasAI.IFM.Shared.MarketData.Diagnostics;

namespace TomasAI.IFM.Shared.MarketData.Diagnostics;

public enum FeedDrainStage
{
    Idle = 0,
    WaitingForNativeSignal = 1,
    ReadingNativeBatch = 2,
    RoutingNativeRecord = 3,
    FlushingPartialBatches = 4,
    PublishingManagedBatch = 5,
    ReadingNativeStatistics = 6,
    Completed = 7,
    Faulted = 8
}
