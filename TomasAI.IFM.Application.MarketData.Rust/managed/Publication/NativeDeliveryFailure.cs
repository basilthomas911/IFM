namespace TomasAI.IFM.Application.MarketData.Rust.Publication;
/// <summary>Identifies which managed delivery path failed so the Host never
/// substitutes option recovery for a failed futures or shared receiver path.</summary>
public sealed class NativeDeliveryFailure(string component,uint laneMask,Exception cause):InvalidOperationException(cause.Message,cause)
{
    /// <summary>Receiver, Primary, OptionQuote or OptionTrade.</summary>
    public string Component{get;}=component;
    /// <summary>Bits 0/1 select GLBX/XCBF futures; zero denotes options only.</summary>
    public uint LaneMask{get;}=laneMask;
}
