namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Retains only the newest owned wire buffer. Published buffers are never pooled or modified.</summary>
sealed class PublicationCache<T> where T : class
{
    readonly object gate=new();
    T? previous;
    byte[] bytes=[];
    public byte[] Read(T value) {
        lock(gate) {
            if(!ReferenceEquals(previous,value)) {
                var encoded=BridgeWire.Serialize(value);
                bytes=encoded;previous=value;
            }
            return bytes;
        }
    }
}
