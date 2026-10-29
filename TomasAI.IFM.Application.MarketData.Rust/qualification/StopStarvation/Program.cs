using System.Net;
using System.Net.Sockets;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;

// Isolated qualification process. Deliberately starve its CLR ThreadPool and
// observe a real native SDK socket closing before releasing the blocked worker.
// Synchronous entry and dedicated peer/delivery threads avoid test dependencies
// on the very scheduler whose starvation is being exercised.
if (args.Length != 1) throw new ArgumentException("Expected absolute native DLL path.");
using var accepted = new ManualResetEventSlim();
using var closed = new ManualResetEventSlim();
using var occupied = new ManualResetEventSlim();
using var release = new ManualResetEventSlim();
Exception? peerError = null;
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
var peer = new Thread(() => {
    try {
        using var client = listener.AcceptTcpClient(); client.ReceiveTimeout = 5000; accepted.Set();
        if (client.GetStream().ReadByte() != -1) throw new InvalidDataException("Unexpected bytes in auth-stall fixture.");
        closed.Set();
    } catch (Exception error) { peerError = error; closed.Set(); }
}) { IsBackground = true, Name = "native-stop-peer" };
peer.Start();
var runtime = new NativeMarketDataRuntime(Path.GetFullPath(args[0]), 1);
runtime.ConfigureLive(new(2026, 10, 26), Guid.NewGuid(), DateTimeOffset.UnixEpoch, false, 4);
var start = runtime.StartLiveLaneAsync(MarketDataLane.OptionsGlbx, new string('x', 32), "127.0.0.1",
    (ushort)((IPEndPoint)listener.LocalEndpoint).Port, ["127.0.0.1:1"],
    [new(["ESZ6"], "trades", "raw_symbol")], [new(42, "ES20261218C6000", "ESZ6", true, null, false, true)], true, TimeSpan.FromMinutes(2));
ThreadPool.GetMaxThreads(out var maximumWorkers, out var maximumIo);
ThreadPool.GetMinThreads(out var minimumWorkers, out var minimumIo);
try
{
    if (!accepted.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Native SDK did not connect.");
    if (!ThreadPool.SetMinThreads(1, minimumIo) || !ThreadPool.SetMaxThreads(1, maximumIo)) throw new InvalidOperationException("Could not configure isolated starvation fixture.");
    ThreadPool.QueueUserWorkItem(_ => { occupied.Set(); release.Wait(); });
    if (!occupied.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Starvation worker did not enter.");
    var stopping = runtime.StopAndDisposeAsync(TimeSpan.FromSeconds(5));
    if (!closed.Wait(TimeSpan.FromSeconds(1))) throw new TimeoutException("Native stop waited behind the starved CLR ThreadPool.");
    if (peerError is not null) throw new InvalidOperationException("Peer close failed.", peerError);
    release.Set();
    stopping.AsTask().GetAwaiter().GetResult();
    try { start.GetAwaiter().GetResult(); throw new InvalidOperationException("Interrupted native start succeeded."); }
    catch (OperationCanceledException) { }
    Console.WriteLine("PASS: native SDK socket closed while the CLR ThreadPool was fully starved; delivery and runtime release completed after worker release.");
}
finally
{
    release.Set(); ThreadPool.SetMaxThreads(maximumWorkers, maximumIo); ThreadPool.SetMinThreads(minimumWorkers, minimumIo);
    runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
    listener.Stop(); if (!peer.Join(TimeSpan.FromSeconds(3))) throw new TimeoutException("Peer did not exit.");
}
