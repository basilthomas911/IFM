using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Framework.MarketData.DataBento.Interop;

internal static partial class NativeMethods
{
    private const string LibraryName = "databento_feed_native";

    [LibraryImport(LibraryName, EntryPoint = "dbf_get_abi_version")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial uint GetAbiVersion();

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_create")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus FeedCreate(
        NativeFeedConfig* config,
        byte* utf8Blob,
        uint utf8BlobBytes,
        out nint feed);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_subscribe_tickers")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus FeedSubscribeTickers(
        SafeDbFeedHandle feed,
        NativeTickerSubscription* subscriptions,
        uint subscriptionCount,
        byte* utf8Blob,
        uint utf8BlobBytes,
        uint timeoutMilliseconds);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_subscribe_option_chain")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus FeedSubscribeOptionChain(
        SafeDbFeedHandle feed,
        NativeOptionChainSubscription* subscription,
        NativeOptionContractSelection* contracts,
        uint contractCount,
        byte* utf8Blob,
        uint utf8BlobBytes,
        uint timeoutMilliseconds);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_allocate_read_buffer64")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus FeedAllocateReadBuffer(
        SafeDbFeedHandle feed,
        uint recordCapacity,
        out MarketRecord64* buffer);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_start")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial DatabentoFeedStatus FeedStart(
        SafeDbFeedHandle feed,
        uint timeoutMilliseconds);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_get_ticker_mapping_counts")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial DatabentoFeedStatus FeedGetTickerMappingCounts(
        SafeDbFeedHandle feed,
        out uint mappingCount,
        out uint utf8BlobBytes);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_copy_ticker_mappings")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus FeedCopyTickerMappings(
        SafeDbFeedHandle feed,
        NativeTickerInstrumentMapping* mappings,
        uint mappingCapacity,
        byte* utf8Blob,
        uint utf8BlobCapacity);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_set_consumer_ready")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial DatabentoFeedStatus FeedSetConsumerReady(
        SafeDbFeedHandle feed,
        uint timeoutMilliseconds);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_wait")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial DatabentoFeedStatus FeedWait(
        SafeDbFeedHandle feed,
        uint timeoutMilliseconds,
        ref NativeWaitResult result);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_read_batch64")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus FeedReadBatch(
        SafeDbFeedHandle feed,
        MarketRecord64* destination,
        uint destinationRecordCapacity,
        ref NativeBatchResult result);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_stop")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial DatabentoFeedStatus FeedStop(
        SafeDbFeedHandle feed,
        uint timeoutMilliseconds);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_free_read_buffer64")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus FeedFreeReadBuffer(
        SafeDbFeedHandle feed,
        MarketRecord64* buffer);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_get_stats")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial DatabentoFeedStatus FeedGetStats(
        SafeDbFeedHandle feed,
        ref NativeFeedStats stats);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_get_last_error")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus FeedGetLastError(
        SafeDbFeedHandle feed,
        byte* utf8Buffer,
        uint utf8BufferCapacity,
        out uint requiredBytes);

    [LibraryImport(LibraryName, EntryPoint = "dbf_feed_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial DatabentoFeedStatus FeedDestroy(nint feed);

    [LibraryImport(LibraryName, EntryPoint = "dbf_contract_details_query")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus ContractDetailsQuery(
        NativeContractQuery* query,
        NativeUtf8Slice* symbols,
        byte* utf8Blob,
        uint utf8BlobBytes,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "dbf_contract_details_result_get_counts")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial DatabentoFeedStatus ContractDetailsResultGetCounts(
        SafeContractDetailsResultHandle result,
        out uint detailCount,
        out uint utf8BlobBytes);

    [LibraryImport(LibraryName, EntryPoint = "dbf_contract_details_result_copy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus ContractDetailsResultCopy(
        SafeContractDetailsResultHandle result,
        NativeContractDetail* details,
        uint detailCapacity,
        byte* utf8Blob,
        uint utf8BlobCapacity);

    [LibraryImport(LibraryName, EntryPoint = "dbf_contract_details_result_get_error")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static unsafe partial DatabentoFeedStatus ContractDetailsResultGetError(
        SafeContractDetailsResultHandle result,
        byte* utf8Buffer,
        uint utf8BufferCapacity,
        out uint requiredBytes);

    [LibraryImport(LibraryName, EntryPoint = "dbf_contract_details_result_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial DatabentoFeedStatus ContractDetailsResultDestroy(nint result);
}
