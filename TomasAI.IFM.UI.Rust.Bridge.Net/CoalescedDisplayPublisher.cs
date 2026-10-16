using System.Threading.Channels;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Coalesces display invalidations, not their source events. A burst has one pending publication.</summary>
sealed class CoalescedDisplayPublisher : IAsyncDisposable
{
    readonly Channel<bool> changes=Channel.CreateBounded<bool>(new BoundedChannelOptions(1){SingleReader=true,FullMode=BoundedChannelFullMode.DropWrite,AllowSynchronousContinuations=false});
    readonly CancellationTokenSource stopping=new();
    readonly Task worker;
    public CoalescedDisplayPublisher(Action publish,Action<Exception> reportError)=>worker=RunAsync(publish,reportError);
    public void Request()=>changes.Writer.TryWrite(true);
    async Task RunAsync(Action publish,Action<Exception> reportError){
        try {
            await foreach(var changed in changes.Reader.ReadAllAsync(stopping.Token).ConfigureAwait(false)){
                await Task.Delay(40,stopping.Token).ConfigureAwait(false);
                while(changes.Reader.TryRead(out _)){}
                try{publish();}catch(Exception e){reportError(e);}
            }
        }catch(OperationCanceledException)when(stopping.IsCancellationRequested){}
    }
    public async ValueTask DisposeAsync(){changes.Writer.TryComplete();await stopping.CancelAsync().ConfigureAwait(false);await worker.ConfigureAwait(false);stopping.Dispose();}
}
