using TomasAI.IFM.Application.MarketData.Databento.Workers;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Explicit publication lifetime for the currently bootstrapping native
/// API epoch. A replacement cannot reuse the preceding publisher's sequences.</summary>
public sealed class NativeSessionPublications(MarketDataHostConnection connection,NativeHostState state)
{
    readonly object gate=new();NativeNatsPublisher? current;
    /// <summary>Returns the most recent epoch's bounded catch-up directory.</summary>
    public NativeNatsPublisher? Current{get{lock(gate)return current;}}
    /// <summary>Starts a prepared API date before native bootstrap emits records.</summary>
    public NativeNatsPublisher Create(DateOnly date,CancellationToken lifetime,IReadOnlyCollection<DatasetSubscriptionManifest>? manifests=null)
    {
        lock(gate)
        {
            state.PrepareDate(date,manifests);
            current=new(connection,date,(role,dataset)=>state.Owner(date,role,dataset),lifetime);
            foreach(var manifest in manifests??[]){current.SetFuturesManifestRevision(manifest.Dataset,manifest.Revision);if(manifest.Dataset=="GLBX.MDP3")current.SetOptionManifestRevision(manifest.Revision);}
            return current;
        }
    }
}
