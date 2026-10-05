using Microsoft.Win32.SafeHandles;

namespace TomasAI.IFM.Framework.MarketData.DataBento.Interop;

internal sealed class SafeContractDetailsResultHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new SafeContractDetailsResultHandle instance.</summary>
    private SafeContractDetailsResultHandle()
        : base(ownsHandle: true)
    {
    }

    /// <summary>Initializes a new SafeContractDetailsResultHandle instance.</summary>
    /// <param name="handle">The native handle whose lifetime this instance owns.</param>
    internal SafeContractDetailsResultHandle(nint handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle() =>
        NativeMethods.ContractDetailsResultDestroy(handle) == DatabentoFeedStatus.Ok;
}
