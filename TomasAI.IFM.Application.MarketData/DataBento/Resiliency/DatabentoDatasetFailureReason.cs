using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;
public enum DatabentoDatasetFailureReason
{
    None = 0,
    SnapshotIncomplete = 1,
    NativeTerminalFailure = 2,
    NativeProducerStopped = 3,
    NativeRingOverrun = 4,
    NativeDrainStalled = 5,
    ManagedChannelBlocked = 6,
    AggregationWorkerStopped = 7,
    AggregationRecordStalled = 8,
    SubscriptionIncomplete = 9,
    DatasetTeardownUnresponsive = 10,
    DatasetQualificationFailed = 11,
    FullPipelineUnhealthy = 12
}
