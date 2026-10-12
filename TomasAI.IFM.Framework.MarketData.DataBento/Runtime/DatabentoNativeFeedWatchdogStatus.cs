using System.Runtime.InteropServices;
using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new DatabentoNativeFeedWatchdogStatus instance.</summary>
/// <param name="FeedInstanceId">The feed instance id.</param>
/// <param name="GenerationId">The generation id.</param>
/// <param name="FeedKind">The feed kind.</param>
/// <param name="MajorStatus">The major status.</param>
/// <param name="State">The store holding the option-chain session state.</param>
/// <param name="TerminalStatus">The terminal status.</param>
/// <param name="ProducerAlive">The producer alive.</param>
/// <param name="ConsumerReady">The consumer ready.</param>
/// <param name="ExpectedSubscriptions">The expected subscriptions.</param>
/// <param name="ReceivedSubscriptions">The received subscriptions.</param>
/// <param name="HeartbeatCount">The heartbeat count.</param>
/// <param name="ProviderMessageCount">The provider message count.</param>
/// <param name="LastHeartbeatMonotonicNanoseconds">The last heartbeat monotonic nanoseconds.</param>
/// <param name="LastProviderMessageMonotonicNanoseconds">The last provider message monotonic nanoseconds.</param>
/// <param name="RecordsProduced">The records produced.</param>
/// <param name="RecordsConsumed">The records consumed.</param>
/// <param name="RingCapacityRecords">The ring capacity records.</param>
/// <param name="RingUsedRecords">The ring used records.</param>
/// <param name="RingHighWaterRecords">The ring high water records.</param>
/// <param name="RingOverruns">The ring overruns.</param>
/// <param name="Dataset">The dataset.</param>
/// <param name="FailureDetail">The failure detail.</param>
public sealed record DatabentoNativeFeedWatchdogStatus(
    ulong FeedInstanceId, ulong GenerationId, uint FeedKind, uint MajorStatus,
    FeedState State, DatabentoFeedStatus TerminalStatus, bool ProducerAlive,
    bool ConsumerReady, uint ExpectedSubscriptions, uint ReceivedSubscriptions,
    ulong HeartbeatCount, ulong ProviderMessageCount,
    ulong LastHeartbeatMonotonicNanoseconds, ulong LastProviderMessageMonotonicNanoseconds,
    ulong RecordsProduced, ulong RecordsConsumed, ulong RingCapacityRecords,
    ulong RingUsedRecords, ulong RingHighWaterRecords, ulong RingOverruns,
    string Dataset, string FailureDetail);
