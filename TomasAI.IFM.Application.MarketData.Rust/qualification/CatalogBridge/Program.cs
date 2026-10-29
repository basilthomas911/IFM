using System.Runtime.CompilerServices;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Shared.MarketData.Definitions;

if(args.Length==3&&args[0]=="--export-option-models"){await CatalogParity.ExportOptions(args[1],args[2]);return;}
if(args.Length==3&&args[0]=="--catalog-parity"){using var fixture=JsonDocument.Parse(await File.ReadAllTextAsync(args[2]));await CatalogParity.Run(Path.GetFullPath(args[1]),fixture.RootElement[0].GetProperty("json").GetString()!);return;}
// Real DLL boundary: provider source identity, cancellation and failed refresh
// semantics. This tool does not use provider credentials or production services.
if (args.Length != 2) throw new ArgumentException("Expected native DLL and definition oracle paths.");
using var corpus = JsonDocument.Parse(File.ReadAllText(args[1]));
await using var runtime = new NativeMarketDataRuntime(Path.GetFullPath(args[0]));
runtime.ConfigureCatalog(2, 131_072);
static async IAsyncEnumerable<CatalogSourceLine> Lines(string source, [EnumeratorCancellation] CancellationToken token = default)
{
    token.ThrowIfCancellationRequested();
    await Task.Yield();
    yield return new("GLBX.MDP3", source);
}
var seed = corpus.RootElement[0].GetProperty("json").GetString()!;
const string id = "ES20261225";
var checkedCases = 0;
foreach (var test in corpus.RootElement.EnumerateArray())
{
    var source = test.GetProperty("json").GetString()!;
    if (test.GetProperty("valid").GetBoolean())
    {
        await runtime.HydrateCatalogAsync(Lines(source));
        // Only compare canonical round-trip records, just as the production
        // mapping prerequisite does. Original parser is the frozen oracle.
        var definition = ExactInstrumentDefinition.Parse("GLBX.MDP3", source);
        var summary = definition.Summary;
        using (var snapshot = runtime.OpenCatalogSnapshot("GLBX.MDP3"))
        {
            if (snapshot.Count != 1 || snapshot.ReadSource(0) != source) throw new InvalidDataException("Pinned native source differs from the original parser.");
            var native = snapshot.ReadDefinition(0);
            if (native.Dataset != definition.Dataset || native.Symbol != definition.RawSymbol || native.Asset != definition.Asset
                || native.Publisher != definition.PublisherId || native.Instrument != definition.InstrumentId || native.Kind != (byte)summary.ContractKind
                || native.Currency != definition.Currency || native.Exchange != definition.Exchange || native.SettlementCurrency != summary.SettlementCurrency
                || native.Underlying != summary.Underlying || native.UnderlyingInstrument != summary.UnderlyingInstrumentId
                || native.ReceivedNanoseconds != definition.ReceivedNanoseconds || native.EventNanoseconds != definition.EventNanoseconds
                || native.Deleted != definition.Deleted || native.Activation != summary.ActivationTimestampNanoseconds || native.Expiration != summary.ExpirationTimestampNanoseconds
                || native.Maturity != summary.MaturityDate || native.Strike != summary.StrikePrice || native.Tick != summary.MinimumPriceIncrement
                || native.TickAmount != summary.MinimumPriceIncrementAmount || native.Multiplier != summary.ContractMultiplier || native.MaturityWeek != summary.MaturityWeek)
                throw new InvalidDataException("Native typed projection differs from unchanged C# definition fields.");
        }
        if ((int)summary.ContractKind == 1
            && definition.Asset == "ES" && summary.ExpirationTimestampNanoseconds == 1798191000000000000)
        {
            if (runtime.ReadCatalogDefinition(id) != source) throw new InvalidDataException("Source bytes changed at the native catalog boundary.");
        }
    }
    else
    {
        await runtime.HydrateCatalogAsync(Lines(seed));
        try { await runtime.HydrateCatalogAsync(Lines(source)); throw new InvalidDataException("Invalid source accepted."); }
        catch (InvalidOperationException) { }
        if (runtime.ReadCatalogDefinition(id) != seed) throw new InvalidDataException("Failed refresh replaced committed source.");
    }
    checkedCases++;
}
await runtime.HydrateCatalogAsync(Lines(seed));
for (var cycle = 0; cycle < 1000; cycle++)
{
    using var cancellation = new CancellationTokenSource();
    async IAsyncEnumerable<CatalogSourceLine> Cancelled([EnumeratorCancellation] CancellationToken token = default)
    {
        yield return new("GLBX.MDP3", seed);
        cancellation.Cancel();
        await Task.Yield();
        token.ThrowIfCancellationRequested();
        yield return new("GLBX.MDP3", seed);
    }
    try { await runtime.HydrateCatalogAsync(Cancelled(), cancellation.Token); throw new InvalidDataException("Cancellation ignored."); }
    catch (OperationCanceledException) { }
    if (runtime.ReadCatalogDefinition(id) != seed) throw new InvalidDataException("Cancelled refresh replaced committed source.");
    await runtime.HydrateCatalogAsync(Lines(seed)); // Prove cancellation released staging.
}
Console.WriteLine($"Catalog bridge passed: {checkedCases} C# oracle cases, 1000 cancelled refreshes followed by successful rehydration; source identity preserved.");

// HTTP -> Rust decoder/repository -> C ABI completion -> managed typed boundary.
// Each connection is owned by the tested native operation, not a server process.
var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
listener.Start();
try
{
    var endpoint = (System.Net.IPEndPoint)listener.LocalEndpoint;
    runtime.ConfigureHistory($"http://127.0.0.1:{endpoint.Port}/", new string('x', 32), ["127.0.0.1:1"]);
    static async Task Request(Stream stream)
    {
        var header = new List<byte>(); var one = new byte[1];
        while (header.Count < 16_384)
        {
            await stream.ReadExactlyAsync(one); header.Add(one[0]);
            if (header.Count >= 4 && header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
        }
        var text = System.Text.Encoding.ASCII.GetString(header.ToArray());
        var length = text.Split("\r\n").Where(s => s.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            .Select(s => int.Parse(s.Split(':')[1].Trim())).DefaultIfEmpty(0).Single();
        await stream.ReadExactlyAsync(new byte[length]);
    }
    static async Task Response(Stream stream, string body, int extra)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {bytes.Length + extra}\r\nX-Warning: {new string('x', 32)} complete provider warning\r\nX-Warning: malformed warning JSON\r\nX-Request-Id: fixture-42\r\nConnection: close\r\n\r\n"));
        await stream.WriteAsync(bytes);
    }
    for (var mode = 0; mode < 12; mode++)
    {
        var selected = mode % 4;
        using var cancellation = new CancellationTokenSource();
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            using (var coverage = await listener.AcceptTcpClientAsync())
            {
                await Request(coverage.GetStream());
                await Response(coverage.GetStream(), """{"start":"1990-01-01T00:00:00Z","end":"2026-12-31T00:00:00Z","schema":{"definition":{"start":"1990-01-01T00:00:00Z","end":"2026-10-25T00:00:00Z"}}}""", 0);
            }
            using var source = await listener.AcceptTcpClientAsync();
            var stream = source.GetStream();
            await Request(stream);
            await Response(stream, seed + "\n" + (selected == 1 ? "{\n" : ""), selected >= 2 ? 100 : 0);
            arrived.SetResult();
            if (selected >= 2)
            {
                var one = new byte[1];
                try
                {
                    if (await stream.ReadAsync(one).AsTask().WaitAsync(TimeSpan.FromSeconds(3)) != 0)
                        throw new InvalidDataException("Unexpected bytes after response.");
                }
                catch (IOException) { /* Windows can signal RST instead of EOF. */ }
            }
        });
        Task<NativeCatalogSnapshot>? acquired = mode >= 8 ? runtime.AcquireExactDefinitionSnapshotAsync("GLBX.MDP3", ["ESZ6"],
            selected == 2 ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(5), cancellation.Token) : mode >= 4 ? runtime.AcquireDefinitionSnapshotAsync("GLBX.MDP3", ["ES.FUT"],
            selected == 2 ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(5), cancellation.Token) : null;
        Task refresh = acquired is not null ? acquired : runtime.RefreshCatalogAsync([new("GLBX.MDP3", ["ES.FUT"])],
            selected == 2 ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(5), cancellation.Token);
        var capturedWarnings = new List<HistoricalWarning>();
        if (selected == 3)
        {
            await arrived.Task.WaitAsync(TimeSpan.FromSeconds(3));
            // A server write is not proof of client receipt. Cancel the stalled
            // body only after both responses' warning headers were parsed.
            using var received = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while(capturedWarnings.Count < 4)
            {
                capturedWarnings.AddRange(runtime.DrainHistoricalWarnings());
                if(capturedWarnings.Count < 4)await Task.Delay(1,received.Token);
            }
            cancellation.Cancel();
        }
        if (selected == 0)
        {
            await refresh;
            if (acquired is not null)
            {
                using var owned = await acquired;
                if (owned.Count != 1 || owned.ReadSource(0) != seed || owned.ReadDefinition(0).Instrument != 42)
                    throw new InvalidDataException("Scoped provider query changed its source/projection.");
            }
        }
        else
        {
            try { await refresh; throw new InvalidDataException("Failing refresh unexpectedly succeeded."); }
            catch (Exception error) when (error is InvalidOperationException or TimeoutException or OperationCanceledException or TomasAI.IFM.Framework.MarketData.DataBento.DatabentoFeedException) { }
        }
        await server.WaitAsync(TimeSpan.FromSeconds(4));
        capturedWarnings.AddRange(runtime.DrainHistoricalWarnings());
        var warnings = capturedWarnings.ToArray();
        if (warnings.Length != 4 || warnings.Any(w => w.Status != 200 || w.RequestId != "fixture-42")
            || warnings.Count(w => w.Text == "[REDACTED] complete provider warning") != 2
            || warnings.Count(w => w.Text == "malformed warning JSON") != 2
            || warnings.Any(w => w.Endpoint.Length == 0 || w.Text.Contains(new string('x', 32), StringComparison.Ordinal))
            || runtime.DrainHistoricalWarnings().Length != 0)
            throw new InvalidDataException($"HTTP warnings lost, duplicated or not redacted across the native boundary; mode={mode}; received={warnings.Length}.");
        if (runtime.ReadCatalogDefinition(id) != seed) throw new InvalidDataException("Provider refresh lost the committed definition.");
        await runtime.HydrateCatalogAsync(Lines(seed));
    }
    Console.WriteLine("Provider catalog bridge passed: refresh/parent/exact HTTP happy, invalid-source, deadline and cancellation; source state preserved, sockets closed, full redacted warnings retained on every outcome.");
}
finally { listener.Stop(); }
await CatalogParity.Run(Path.GetFullPath(args[0]), seed);
