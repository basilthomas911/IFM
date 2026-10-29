using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;

if (args.Length != 2) throw new ArgumentException("Expected absolute native DLL path and FrameworkOracle.json path.");
var library = Path.GetFullPath(args[0]);
using var fixture = JsonDocument.Parse(File.ReadAllText(args[1]));
var cases = fixture.RootElement.GetProperty("Treasury").EnumerateArray().ToArray();

await using (var runtime = new NativeMarketDataRuntime(library))
{
    // Batches exercise concurrent delivery without deliberately exceeding the
    // 256-operation hard bound. Every expected value was produced by unchanged C#.
    foreach (var batch in cases.Chunk(64))
        await Task.WhenAll(batch.Select(async item =>
        {
            var input = BitConverter.UInt64BitsToDouble(item.GetProperty("HalfYieldBits").GetUInt64());
            var succeeded = item.GetProperty("Succeeded").GetBoolean();
            try
            {
                var actual = await runtime.ConvertTreasuryRateAsync(input, TimeSpan.FromSeconds(2));
                if (!succeeded || BitConverter.DoubleToUInt64Bits(actual) != item.GetProperty("ContinuousRateBits").GetUInt64())
                    throw new InvalidDataException("Asynchronous native result differs from the C# corpus.");
            }
            catch (ArgumentOutOfRangeException) when (!succeeded) { }
        }));

    try
    {
        await runtime.ConvertTreasuryRateAsync(0.01, TimeSpan.Zero);
        throw new InvalidDataException("Already-expired operation was executed.");
    }
    catch (TimeoutException) { }
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    try
    {
        await runtime.ConvertTreasuryRateAsync(0.01, TimeSpan.FromSeconds(2), cancelled.Token);
        throw new InvalidDataException("Pre-cancelled operation was accepted.");
    }
    catch (OperationCanceledException) { }
}

// Repeat actual native runtime creation, concurrent completion/cancellation and
// disposal. Each cycle must release its registry identity, otherwise the fifth
// construction hits the native four-runtime limit and fails this qualification.
for (var cycle = 0; cycle < 1000; cycle++)
{
    var runtime = new NativeMarketDataRuntime(library, workerThreads: 1);
    using var cancellation = new CancellationTokenSource();
    var requests = Enumerable.Range(0, 16).Select(_ => runtime.ConvertTreasuryRateAsync(0.01, TimeSpan.FromSeconds(2), cancellation.Token)).ToArray();
    cancellation.Cancel();
    var firstDispose = runtime.StopAndDisposeAsync(TimeSpan.FromSeconds(2)).AsTask();
    var secondDispose = runtime.DisposeAsync().AsTask();
    if (!ReferenceEquals(firstDispose, secondDispose)) throw new InvalidDataException("Disposal renewed its operation/budget.");
    foreach (var request in requests)
    {
        try { await request; }
        catch (OperationCanceledException) { }
    }
    await firstDispose;
    try
    {
        await runtime.ConvertTreasuryRateAsync(0.01, TimeSpan.FromSeconds(2));
        throw new InvalidDataException("Disposed runtime admitted work.");
    }
    catch (ObjectDisposedException) { }
}

Console.WriteLine($"Managed-to-native asynchronous parity: {cases.Length} exact C# corpus matches; expired/pre-cancelled work rejected; 1000 create/cancel/dispose cycles passed.");
