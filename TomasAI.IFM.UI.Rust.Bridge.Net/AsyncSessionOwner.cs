using System.Threading.Channels;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>One asynchronous consumer per session. Accepted work drains before shutdown; no command is dropped.</summary>
sealed class AsyncSessionOwner : IAsyncDisposable
{
    readonly record struct Work(Func<Task> Execute,TaskCompletionSource Completion);
    readonly Channel<Work> queue=Channel.CreateBounded<Work>(new BoundedChannelOptions(1) {
        SingleReader=true, SingleWriter=false, AllowSynchronousContinuations=false,
        FullMode=BoundedChannelFullMode.Wait
    });
    readonly Task worker;
    public AsyncSessionOwner()=>worker=RunAsync();
    public Task Enqueue(Func<Task> execute) {
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if(!queue.Writer.TryWrite(new(execute,completion)))throw new InvalidOperationException("Session owner is full or closed");
        return completion.Task;
    }
    async Task RunAsync() {
        await foreach(var work in queue.Reader.ReadAllAsync().ConfigureAwait(false)) {
            try {await work.Execute().ConfigureAwait(false);work.Completion.SetResult();}
            catch(Exception e){work.Completion.SetException(e);}
        }
    }
    public async ValueTask DisposeAsync(){queue.Writer.TryComplete();await worker.ConfigureAwait(false);}
}
