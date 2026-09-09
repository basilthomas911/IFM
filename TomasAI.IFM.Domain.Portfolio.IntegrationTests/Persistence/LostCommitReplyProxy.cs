using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

/// <summary>Test-owned loopback PostgreSQL proxy: drops the first COMMIT response after PostgreSQL has committed.</summary>
internal sealed class LostCommitReplyProxy : IAsyncDisposable
{
    readonly TcpListener listener=new(IPAddress.Loopback,0);
    readonly CancellationTokenSource shutdown=new();
    readonly ConcurrentBag<Task> connections=[];
    readonly bool refuseRecovery;
    readonly Task accepting;
    int dropped;
    public bool Dropped=>Volatile.Read(ref dropped)==1;
    public int Port=>((IPEndPoint)listener.LocalEndpoint).Port;

    public LostCommitReplyProxy(bool refuseRecovery=false)
    {
        this.refuseRecovery=refuseRecovery;
        listener.Start(); accepting=AcceptAsync();
    }
    async Task AcceptAsync()
    {
        try
        {
            while(!shutdown.IsCancellationRequested)
            {
                var client=await listener.AcceptTcpClientAsync(shutdown.Token);
                if(refuseRecovery && Dropped) { client.Dispose(); continue; }
                connections.Add(ForwardAsync(client));
            }
        }
        catch(OperationCanceledException) when(shutdown.IsCancellationRequested) { }
        catch(SocketException) when(shutdown.IsCancellationRequested) { }
    }
    async Task ForwardAsync(TcpClient client)
    {
        using(client)
        using(var upstream=new TcpClient())
        using(var stop=CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token))
        {
            Task? requests=null,replies=null;
            try
            {
                await upstream.ConnectAsync(IPAddress.Loopback,5432,stop.Token);
                var incoming=client.GetStream(); var outgoing=upstream.GetStream();
                // Tests explicitly disable TLS only for this loopback connection. Never record authentication bytes.
                var prefix=new byte[4]; await incoming.ReadExactlyAsync(prefix,stop.Token);
                var length=BinaryPrimitives.ReadInt32BigEndian(prefix);
                if(length is <8 or >65536) throw new InvalidDataException("Invalid PostgreSQL startup length.");
                var startup=new byte[length-4]; await incoming.ReadExactlyAsync(startup,stop.Token);
                await outgoing.WriteAsync(prefix,stop.Token); await outgoing.WriteAsync(startup,stop.Token);
                requests=PumpAsync(incoming,outgoing,false,stop.Token);
                replies=PumpAsync(outgoing,incoming,true,stop.Token);
                await Task.WhenAny(requests,replies);
            }
            catch(Exception error) when(error is IOException or SocketException or OperationCanceledException) { }
            finally
            {
                await stop.CancelAsync(); client.Close(); upstream.Close();
                foreach(var task in new[] { requests,replies })
                    if(task is not null)
                        try { await task; } catch(Exception error) when(error is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
            }
        }
    }
    async Task PumpAsync(NetworkStream source,NetworkStream destination,bool backend,CancellationToken token)
    {
        var header=new byte[5];
        while(!token.IsCancellationRequested)
        {
            await source.ReadExactlyAsync(header,token);
            var length=BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1));
            if(length is <4 or >16*1024*1024) throw new InvalidDataException("Invalid PostgreSQL frame length.");
            var content=new byte[length-4]; await source.ReadExactlyAsync(content,token);
            if(backend && header[0]==(byte)'C' && Encoding.ASCII.GetString(content)=="COMMIT\0" &&
                Interlocked.CompareExchange(ref dropped,1,0)==0) return;
            await destination.WriteAsync(header,token); await destination.WriteAsync(content,token);
        }
    }
    public async ValueTask DisposeAsync()
    {
        await shutdown.CancelAsync(); listener.Stop();
        await accepting;
        await Task.WhenAll(connections).WaitAsync(TimeSpan.FromSeconds(5));
        shutdown.Dispose();
    }
}
