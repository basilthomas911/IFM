using TomasAI.IFM.Application.MarketData.Rust.Interop;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

public sealed partial class NativeMarketDataRuntime
{
    // Caller storage is reusable only after the complete binary context has been
    // deserialized/copied. Native records own no borrowed application memory.
    internal unsafe int DrainOptionScopeTradePublications(string scope,Guid generation,Span<NativeOptionPublication> destination,Span<byte> manifests,out int written)
    {
        if(destination.Length>16||manifests.Length>4*1024*1024)throw new ArgumentOutOfRangeException(nameof(destination));
        Span<byte> bytes=stackalloc byte[512];var length=ScopeBytes(scope,bytes);var expected=GuidBits(generation);
        lock(gate)
        {
            EnsureLiveAccess();uint count,used;
            fixed(byte* name=bytes)fixed(NativeOptionPublication* records=destination)fixed(byte* data=manifests)
                NativeMarketDataLibrary.Check(library.ScopeTradePublications(handle.Identity,name,(uint)length,expected.Low,expected.High,816,
                    records,(uint)destination.Length,data,(uint)manifests.Length,out count,out used),"drain source-time option trades");
            if(count>destination.Length||used>manifests.Length)throw new InvalidDataException("Native option trade buffer exceeded caller capacity.");
            for(var index=0;index<count;index++)
            {
                ref var row=ref destination[index];var value=row.Publication;
                if(row.Size!=816||row.Flags>15||value.Size!=656||value.Source.Size!=128||value.Source.Lane!=MarketDataLane.OptionsGlbx
                    ||value.Source.Kind is <1 or >6||value.Update>6||value.Reserved!=0||value.Flags>63||(value.Update>=5)!=(value.Source.Kind>=5)||value.Source.Kind>=5&&value.Update!=value.Source.Kind
                    ||(ulong)row.ManifestOffset+row.ManifestLength>used||row.ManifestLength>4*1024*1024
                    ||(row.Flags&4)==0&&row.ManifestLength!=0||(row.Flags&4)!=0&&row.ManifestLength==0
                    ||(row.Flags&1)!=0&&((row.Flags&8)==0||value.Source.Kind!=2))throw new InvalidDataException("Malformed source-time option trade publication.");
            }
            written=(int)used;return (int)count;
        }
    }
}
