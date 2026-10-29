using System.Runtime.InteropServices;
namespace TomasAI.IFM.Application.MarketData.Rust.Interop;
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeHistoricalRecord
{
    internal uint Size,Abi,Kind,Schema,Instrument;
    internal ushort Publisher,Conditions;
    internal long Event,Sequence,Open,High,Low,Price;
    internal ulong Quantity;
    internal byte Action,Side;
    internal fixed byte Reserved[6];
    internal fixed byte Symbol[32];
}
internal sealed partial class NativeMarketDataLibrary
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]internal unsafe delegate int HistoryRangeCall(ulong handle,ulong operation,byte* input,uint length,uint capacity,ulong budget);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]internal unsafe delegate int HistoryRecordsCall(ulong handle,ulong operation,NativeHistoricalRecord* records,uint capacity,out uint count,out uint ended);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]internal unsafe delegate int HistoryFilePushCall(ulong handle,ulong operation,byte* input,uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]internal unsafe delegate int HistoryDownloadCall(ulong handle,ulong operation,byte* input,uint length,ulong budget);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]internal unsafe delegate int HistoryDownloadTakeCall(ulong handle,ulong operation,byte* output,uint capacity,out uint length,out uint ended);
    internal HistoryDownloadCall HistoryDownload=null!;
    internal HistoryDownloadTakeCall HistoryDownloadTake=null!;
    internal HistoryRangeCall HistoryFile=null!;
    internal HistoryFilePushCall HistoryFilePush=null!;
    internal HistoryRangeCall HistoryRange=null!;
    internal HistoryRangeCall HistoryReplay=null!;
    internal HistoryRecordsCall HistoryRecords=null!;
    internal ReferenceDiscardCall HistoryReaderClose=null!;
    void BindHistorical(nint module)
    {
        if(Marshal.SizeOf<NativeHistoricalRecord>()!=120)throw new InvalidDataException("Native historical record ABI mismatch.");
        T Bind<T>(string name)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module,name));
        HistoryDownload=Bind<HistoryDownloadCall>("ifm_market_data_history_download_open_v1");
        HistoryDownloadTake=Bind<HistoryDownloadTakeCall>("ifm_market_data_history_download_take_v1");
        HistoryFile=Bind<HistoryRangeCall>("ifm_market_data_history_file_open_v1");
        HistoryFilePush=Bind<HistoryFilePushCall>("ifm_market_data_history_file_push_v1");
        HistoryRange=Bind<HistoryRangeCall>("ifm_market_data_history_range_open_v1");
        HistoryReplay=Bind<HistoryRangeCall>("ifm_market_data_history_replay_open_v1");
        HistoryRecords=Bind<HistoryRecordsCall>("ifm_market_data_history_records_v1");
        HistoryReaderClose=Bind<ReferenceDiscardCall>("ifm_market_data_history_reader_close_v1");
    }
}
