using System.Runtime.InteropServices;

namespace TomasAI.IFM.Application.MarketData.Rust.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeCatalogQuery { internal uint Size, Reserved; internal ulong Token; internal uint Count, ReservedTail; }

internal sealed partial class NativeMarketDataLibrary
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CatalogConfigureCall(ulong handle, uint rows, uint bytes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CatalogBeginCall(ulong handle, out ulong token);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int CatalogAppendCall(ulong handle, ulong token, byte* dataset, uint datasetLength, byte* source, uint sourceLength);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CatalogFinishCall(ulong handle, ulong token, uint action, out ulong revision);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int CatalogResolveCall(ulong handle, ulong revision, byte* id, uint idLength, byte* output, uint capacity, out uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int HistoryConfigureCall(ulong handle, byte* input, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int HistoryWarningCall(ulong handle, byte* output, uint capacity, out uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int CatalogRefreshCall(ulong handle, ulong operation, byte* input, uint length, ulong budgetMs);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]internal unsafe delegate int DefinitionProjectCall(byte* dataset,uint datasetLength,byte* source,uint sourceLength,byte* output,uint capacity,out uint length);
    internal DefinitionProjectCall DefinitionProject=null!;
    internal CatalogRefreshCall DefinitionsOpen=null!;
    internal CatalogConfigureCall CatalogConfigure = null!;
    internal CatalogBeginCall CatalogBegin = null!;
    internal CatalogAppendCall CatalogAppend = null!;
    internal CatalogFinishCall CatalogFinish = null!;
    internal CatalogResolveCall CatalogResolve = null!;
    internal HistoryConfigureCall HistoryConfigure = null!;
    internal HistoryWarningCall HistoryWarning = null!;
    internal CatalogRefreshCall CatalogRefresh = null!;
    internal CatalogRefreshCall DefinitionsQuery = null!;
    internal CatalogBeginCall CatalogRevision = null!;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int CatalogQueryCall(ulong handle, ulong revision, byte* input, uint length, out NativeCatalogQuery query);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int CatalogQueryRowCall(ulong handle, ulong token, uint index, uint format, byte* output, uint capacity, out uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CatalogQueryCloseCall(ulong handle, ulong token);
    internal CatalogQueryCall CatalogQuery = null!;
    internal CatalogQueryRowCall CatalogQueryRow = null!;
    internal CatalogQueryCloseCall CatalogQueryClose = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal unsafe delegate int CatalogCurrentCall(ulong handle, ulong token, byte* input, uint length, byte* output, uint capacity, out uint required);
    internal CatalogCurrentCall? CatalogCurrent;
    internal CatalogCurrentCall? CatalogOptionModels;
    void BindCatalog(nint module)
    {
        T Bind<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));
        CatalogCurrent=NativeLibrary.TryGetExport(module,"ifm_market_data_catalog_current_v1",out var current)
            ? Marshal.GetDelegateForFunctionPointer<CatalogCurrentCall>(current) : null;
        CatalogOptionModels=NativeLibrary.TryGetExport(module,"ifm_market_data_catalog_option_models_v1",out var models)?Marshal.GetDelegateForFunctionPointer<CatalogCurrentCall>(models):null;
        DefinitionProject=Bind<DefinitionProjectCall>("ifm_market_data_definition_project_v1");
        DefinitionsOpen=Bind<CatalogRefreshCall>("ifm_market_data_definitions_open_v1");
        CatalogConfigure = Bind<CatalogConfigureCall>("ifm_market_data_catalog_configure_v1");
        CatalogBegin = Bind<CatalogBeginCall>("ifm_market_data_catalog_begin_v1");
        CatalogAppend = Bind<CatalogAppendCall>("ifm_market_data_catalog_append_v1");
        CatalogFinish = Bind<CatalogFinishCall>("ifm_market_data_catalog_finish_v1");
        CatalogResolve = Bind<CatalogResolveCall>("ifm_market_data_catalog_resolve_v1");
        HistoryConfigure = Bind<HistoryConfigureCall>("ifm_market_data_history_configure_v1");
        HistoryWarning = Bind<HistoryWarningCall>("ifm_market_data_history_warning_v1");
        CatalogRefresh = Bind<CatalogRefreshCall>("ifm_market_data_catalog_refresh_v1");
        DefinitionsQuery = Bind<CatalogRefreshCall>("ifm_market_data_definitions_query_v1");
        CatalogRevision = Bind<CatalogBeginCall>("ifm_market_data_catalog_revision_v1");
        if (Marshal.SizeOf<NativeCatalogQuery>() != 24) throw new InvalidDataException("Native catalog query layout mismatch.");
        CatalogQuery = Bind<CatalogQueryCall>("ifm_market_data_catalog_query_v1");
        CatalogQueryRow = Bind<CatalogQueryRowCall>("ifm_market_data_catalog_query_row_v1");
        CatalogQueryClose = Bind<CatalogQueryCloseCall>("ifm_market_data_catalog_query_close_v1");
    }
}
