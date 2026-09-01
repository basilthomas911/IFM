using System.Runtime.InteropServices;

namespace DatabentoFeed.Native.Interop;

public sealed unsafe class NativeApi : IDisposable
{
    public static readonly string[] CanonicalExports =
    [
        "dbf_get_abi_version", "dbf_feed_create", "dbf_feed_subscribe_tickers",
        "dbf_feed_subscribe_option_chain", "dbf_feed_allocate_read_buffer64",
        "dbf_feed_start", "dbf_feed_get_ticker_mapping_counts",
        "dbf_feed_copy_ticker_mappings", "dbf_feed_set_consumer_ready", "dbf_feed_wait",
        "dbf_feed_read_batch64", "dbf_feed_stop", "dbf_feed_free_read_buffer64",
        "dbf_feed_get_stats", "dbf_feed_get_last_error", "dbf_feed_destroy",
        "dbf_contract_details_query", "dbf_contract_details_result_get_counts",
        "dbf_contract_details_result_copy", "dbf_contract_details_result_get_error",
        "dbf_contract_details_result_destroy", "dbf_get_latest_price",
        "dbf_historical_estimate", "dbf_historical_batch_submit",
        "dbf_historical_batch_get_status", "dbf_historical_batch_list_files",
        "dbf_historical_batch_download_file", "dbf_historical_range_open",
        "dbf_historical_file_open", "dbf_historical_result_get_payload",
        "dbf_historical_result_get_next_batch", "dbf_historical_result_get_error",
        "dbf_historical_result_destroy"
    ];

    private nint _library;
    public string Path { get; }

    public NativeApi(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        _library = NativeLibrary.Load(Path);
        GetAbiVersion = Load<GetAbiVersionFn>(CanonicalExports[0]);
        FeedCreate = Load<FeedCreateFn>(CanonicalExports[1]);
        SubscribeTickers = Load<SubscribeTickersFn>(CanonicalExports[2]);
        SubscribeOptionChain = Load<SubscribeOptionChainFn>(CanonicalExports[3]);
        AllocateReadBuffer = Load<AllocateReadBufferFn>(CanonicalExports[4]);
        Start = Load<FeedTimeoutFn>(CanonicalExports[5]);
        GetMappingCounts = Load<GetMappingCountsFn>(CanonicalExports[6]);
        CopyMappings = Load<CopyMappingsFn>(CanonicalExports[7]);
        SetConsumerReady = Load<FeedTimeoutFn>(CanonicalExports[8]);
        Wait = Load<WaitFn>(CanonicalExports[9]);
        ReadBatch = Load<ReadBatchFn>(CanonicalExports[10]);
        Stop = Load<FeedTimeoutFn>(CanonicalExports[11]);
        FreeReadBuffer = Load<FreeReadBufferFn>(CanonicalExports[12]);
        GetStats = Load<GetStatsFn>(CanonicalExports[13]);
        GetLastError = Load<GetLastErrorFn>(CanonicalExports[14]);
        Destroy = Load<DestroyFn>(CanonicalExports[15]);
        ContractQuery = Load<ContractQueryFn>(CanonicalExports[16]);
        ContractGetCounts = Load<ContractGetCountsFn>(CanonicalExports[17]);
        ContractCopy = Load<ContractCopyFn>(CanonicalExports[18]);
        ContractGetError = Load<ContractGetErrorFn>(CanonicalExports[19]);
        ContractDestroy = Load<DestroyFn>(CanonicalExports[20]);
        GetLatestPrice = Load<GetLatestPriceFn>(CanonicalExports[21]);
        HistoricalEstimate = Load<HistoricalEstimateFn>(CanonicalExports[22]);
        HistoricalBatchSubmit = Load<HistoricalOpenFn>(CanonicalExports[23]);
        HistoricalBatchGetStatus = Load<HistoricalJobQueryFn>(CanonicalExports[24]);
        HistoricalBatchListFiles = Load<HistoricalJobQueryFn>(CanonicalExports[25]);
        HistoricalBatchDownloadFile = Load<HistoricalDownloadFn>(CanonicalExports[26]);
        HistoricalRangeOpen = Load<HistoricalOpenFn>(CanonicalExports[27]);
        HistoricalFileOpen = Load<HistoricalFileOpenFn>(CanonicalExports[28]);
        HistoricalGetPayload = Load<HistoricalTextFn>(CanonicalExports[29]);
        HistoricalGetNextBatch = Load<HistoricalNextBatchFn>(CanonicalExports[30]);
        HistoricalGetError = Load<HistoricalTextFn>(CanonicalExports[31]);
        HistoricalDestroy = Load<DestroyFn>(CanonicalExports[32]);
    }

    private T Load<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, name));

    public bool HasExport(string name) => NativeLibrary.TryGetExport(_library, name, out _);
    public GetAbiVersionFn GetAbiVersion { get; }
    public FeedCreateFn FeedCreate { get; }
    public SubscribeTickersFn SubscribeTickers { get; }
    public SubscribeOptionChainFn SubscribeOptionChain { get; }
    public AllocateReadBufferFn AllocateReadBuffer { get; }
    public FeedTimeoutFn Start { get; }
    public GetMappingCountsFn GetMappingCounts { get; }
    public CopyMappingsFn CopyMappings { get; }
    public FeedTimeoutFn SetConsumerReady { get; }
    public WaitFn Wait { get; }
    public ReadBatchFn ReadBatch { get; }
    public FeedTimeoutFn Stop { get; }
    public FreeReadBufferFn FreeReadBuffer { get; }
    public GetStatsFn GetStats { get; }
    public GetLastErrorFn GetLastError { get; }
    public DestroyFn Destroy { get; }
    public ContractQueryFn ContractQuery { get; }
    public ContractGetCountsFn ContractGetCounts { get; }
    public ContractCopyFn ContractCopy { get; }
    public ContractGetErrorFn ContractGetError { get; }
    public DestroyFn ContractDestroy { get; }
    public GetLatestPriceFn GetLatestPrice { get; }
    public HistoricalEstimateFn HistoricalEstimate { get; }
    public HistoricalOpenFn HistoricalBatchSubmit { get; }
    public HistoricalJobQueryFn HistoricalBatchGetStatus { get; }
    public HistoricalJobQueryFn HistoricalBatchListFiles { get; }
    public HistoricalDownloadFn HistoricalBatchDownloadFile { get; }
    public HistoricalOpenFn HistoricalRangeOpen { get; }
    public HistoricalFileOpenFn HistoricalFileOpen { get; }
    public HistoricalTextFn HistoricalGetPayload { get; }
    public HistoricalNextBatchFn HistoricalGetNextBatch { get; }
    public HistoricalTextFn HistoricalGetError { get; }
    public DestroyFn HistoricalDestroy { get; }

    public void Dispose()
    {
        if (_library != 0) NativeLibrary.Free(_library);
        _library = 0;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint GetAbiVersionFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int FeedCreateFn(FeedConfigV1* config, byte* blob, uint bytes, nint* feed);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int SubscribeTickersFn(nint feed, TickerSubscriptionV1* subscriptions, uint count, byte* blob, uint bytes, uint timeoutMs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int SubscribeOptionChainFn(nint feed, OptionChainSubscriptionV1* subscription, OptionContractSelectionV1* contracts, uint count, byte* blob, uint bytes, uint timeoutMs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int AllocateReadBufferFn(nint feed, uint capacity, MarketRecord64** buffer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int FeedTimeoutFn(nint feed, uint timeoutMs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int GetMappingCountsFn(nint feed, uint* count, uint* bytes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int CopyMappingsFn(nint feed, TickerInstrumentMappingV1* mappings, uint mappingCapacity, byte* blob, uint blobCapacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int WaitFn(nint feed, uint timeoutMs, WaitResultV1* result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ReadBatchFn(nint feed, MarketRecord64* destination, uint capacity, BatchResultV1* result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int FreeReadBufferFn(nint feed, MarketRecord64* buffer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int GetStatsFn(nint feed, StatsV1* stats);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int GetLastErrorFn(nint feed, byte* buffer, uint capacity, uint* requiredBytes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int DestroyFn(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ContractQueryFn(ContractQueryV1* query, Utf8SliceV1* symbols, byte* blob, uint bytes, nint* result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ContractGetCountsFn(nint result, uint* detailCount, uint* bytes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ContractCopyFn(nint result, ContractDetailV1* details, uint detailCapacity, byte* blob, uint blobCapacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int ContractGetErrorFn(nint result, byte* buffer, uint capacity, uint* requiredBytes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int GetLatestPriceFn(LatestPriceRequestV1* request, uint timeoutMs, LatestPriceResult64* result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int HistoricalEstimateFn(HistoricalRequestV1* request, Utf8SliceV1* symbols, byte* blob, uint bytes, HistoricalEstimateV1* estimate);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int HistoricalOpenFn(HistoricalRequestV1* request, Utf8SliceV1* symbols, byte* blob, uint bytes, nint* result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int HistoricalJobQueryFn(byte* jobId, uint jobIdBytes, nint* result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int HistoricalDownloadFn(byte* jobId, uint jobIdBytes, byte* fileName, uint fileNameBytes, byte* destinationPath, uint destinationPathBytes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int HistoricalFileOpenFn(byte* filePath, uint filePathBytes, uint schema, nint* result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int HistoricalTextFn(nint result, byte* buffer, uint capacity, uint* requiredBytes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int HistoricalNextBatchFn(nint result, HistoricalRecord120* records, uint capacity, HistoricalBatchV1* batch);
}
