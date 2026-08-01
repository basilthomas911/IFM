using Microsoft.Win32.SafeHandles;

namespace TomasAI.IFM.Framework.MarketData.DataBento.Interop;

internal sealed class SafeContractDetailsResultHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeContractDetailsResultHandle()
        : base(ownsHandle: true)
    {
    }

    internal SafeContractDetailsResultHandle(nint handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle() =>
        NativeMethods.ContractDetailsResultDestroy(handle) == DatabentoFeedStatus.Ok;
}
