using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using NATS.Client.Core;
using NATS.Client.JetStream;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.MarketData.Storage;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Explicit offline child-host runner. Original external inputs enter the real NATS handlers; no financial owner is substituted.</summary>
public static class OriginalSessionReplay
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public sealed record Configuration(int Version, string HostConfiguration, string HostConfigurationSha256,
        string ApiAssembly, string ApiAssemblySha256, string NativeLibrary, string NativeLibrarySha256,
        string NativeCheckpointCertificate, string NativeCheckpointCertificateSha256,
        string WorkingArchiveDirectory, string ArchiveSessionId, string OutputDirectory,
        string[] RootRealtimeVerbs, string AccountObservationMode, long MaximumRecords = 10_000_000,
        int MaximumReplaySeconds = 180000, string? ArchiveCopyManifest = null, string? ArchiveCopyManifestSha256 = null, string? NativePrefixCopyManifest = null, string? NativePrefixCopyManifestSha256 = null, string? InitialArchiveBoundary = null, string? InitialArchiveBoundarySha256 = null, bool UseCertifiedSharedArchive = false);

    public static async Task RunAsync(string path)
    {
        using var runDocument = ReadJson(path, 4 * 1024 * 1024);
        var config = runDocument.RootElement.Deserialize<Configuration>(Json) ?? throw new InvalidDataException("Missing replay configuration.");
        if (config.Version != 1 || config.MaximumRecords is < 1 or > 100_000_000 || config.MaximumReplaySeconds is < 1 or > 200000)
            throw new InvalidDataException("Invalid finite replay bounds.");
        Verify(config.HostConfiguration, config.HostConfigurationSha256); Verify(config.ApiAssembly, config.ApiAssemblySha256);
        Verify(config.NativeLibrary, config.NativeLibrarySha256); Verify(config.NativeCheckpointCertificate, config.NativeCheckpointCertificateSha256);
        var isolatedMode = new HistoricalSessionReplay(config.HostConfiguration, "Test");
        if (isolatedMode.NativeCheckpointCertificatePath != Path.GetFullPath(config.NativeCheckpointCertificate))
            throw new InvalidDataException("Replay host and source runner must bind the same restored native checkpoint.");
        if (!bool.TryParse(isolatedMode.Settings.GetValueOrDefault("Actors:RealtimeMailboxes:Enabled"), out var transportEnabled) || !transportEnabled)
            throw new InvalidDataException("Confirmed-source session replay requires the isolated Memory/File mailbox backend.");
        using var hostDocument = ReadJson(config.HostConfiguration, 4 * 1024 * 1024);
        var host = hostDocument.RootElement;
        var apiUrl = host.GetProperty("httpUrl").GetString()!; var natsUrl = host.GetProperty("natsUrl").GetString()!;
        RequireLoopback(apiUrl, "http"); RequireLoopback(natsUrl, "nats");
        var scope = "ifm." + host.GetProperty("scope").GetString()! + ".marketdata.v1";
        var token = Environment.GetEnvironmentVariable("IFM_REPLAY_CONTROL_TOKEN");
        if (token is not { Length: >= 32 and <= 256 }) throw new InvalidDataException("Protected replay token is missing.");
        using var nativeDocument = ReadJson(config.NativeCheckpointCertificate, 65536);
        var native = nativeDocument.RootElement;
        if (native.GetProperty("Version").GetInt32() != 1 || native.GetProperty("Kind").GetString() != "FrozenNativeAndBackendSnapshot")
            throw new InvalidDataException("Original native/backend checkpoint is required.");
        var nativeRoot = Path.GetDirectoryName(Path.GetFullPath(config.NativeCheckpointCertificate))!;
        var checkpoint = Child(nativeRoot, native.GetProperty("CheckpointFile").GetString()!);
        var snapshotPath = Child(nativeRoot, native.GetProperty("BackendSnapshotFile").GetString()!);
        Verify(snapshotPath, native.GetProperty("BackendSnapshotSha256").GetString()!);
        if (new FileInfo(snapshotPath).Length != native.GetProperty("BackendSnapshotBytes").GetInt64())
            throw new InvalidDataException("Original backend snapshot length differs from its certificate.");
        var restoreRoot = Path.GetDirectoryName(Path.GetFullPath(host.GetProperty("restoreCertificatePath").GetString()!))!;
        PrepareNativePrefixes(config, native, nativeRoot, restoreRoot);
        Environment.SetEnvironmentVariable("IFM_SESSION_CAPTURE_BOUNDARY_ENABLED", "1"); // dedicated offline driver process only
        var staged = NativeMarketDataRuntime.StageNativeCheckpoint(Path.GetFullPath(config.NativeLibrary), native.GetProperty("Scope").GetString()!,
            checkpoint, native.GetProperty("CheckpointSha256").GetString()!);
        if (staged.Bytes != native.GetProperty("CheckpointBytes").GetInt64()) throw new InvalidDataException("Native checkpoint length differs from its certificate.");
        NativeMarketDataRuntime.ConfigureNativeCheckpointRestoreRoot(Path.GetFullPath(config.NativeLibrary), native.GetProperty("Scope").GetString()!, restoreRoot);
        var baseline = File.ReadAllBytes(snapshotPath);
        var output = Path.GetFullPath(config.OutputDirectory);
        if (Directory.Exists(output)) throw new IOException("Replay requires a new output directory.");
        Directory.CreateDirectory(output);
        PrepareArchive(config, restoreRoot);
        var initialBoundary=ReadInitialBoundary(config,host,restoreRoot);
        var archiveEpoch=Path.Combine(Path.GetFullPath(config.WorkingArchiveDirectory),"archive_"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(config.ArchiveSessionId)))[..24]);
        var initialOrdinal=RealtimeSessionArchiveBoundaryVerifier.VerifyInitial(archiveEpoch,initialBoundary);
        if(initialBoundary.ArchiveSessionId!=config.ArchiveSessionId)throw new InvalidDataException("Initial owner floor belongs to another archive session.");
        // Working archive is a verified exported copy, never the live recorder. Opening validates every original frame/receipt.
        await using var archive = new DurableRealtimeSessionArchive(new() { Enabled = true, Directory = config.WorkingArchiveDirectory,
            SessionId = config.ArchiveSessionId, MaximumArchiveBytes = 128L * 1024 * 1024 * 1024, MaximumReceiptBytes = 16L * 1024 * 1024 * 1024 });
        long checkedRecords = 0; DateTimeOffset? first = null, previous = null;
        foreach (var entry in archive.ReadCommitted())
        {
            if (++checkedRecords > config.MaximumRecords)throw new InvalidDataException("Original archive exceeds its finite record bound.");
            if(entry.Ordinal<=initialOrdinal)continue;
            if (entry.RecordedAtUtc is not { } arrival || previous > arrival)
                throw new InvalidDataException("Replay requires bounded original archive-v4 admission timestamps in order.");
            first ??= arrival; previous = arrival;
        }
        if (checkedRecords<initialOrdinal||first is null) throw new InvalidDataException("Original source archive has no inputs after its certified initial owner floor.");
        ReplayMonitoringAuthority.ValidateAgainstArchive(archive,host,restoreRoot,config.ArchiveCopyManifestSha256!,config.InitialArchiveBoundarySha256!,initialOrdinal);
        var account = config.AccountObservationMode;
        if (account is not ("RegenerateFromFileEmulator" or "RecordedExternalInput")) throw new InvalidDataException("Explicit account observation authority is required.");
        var roots = config.RootRealtimeVerbs.ToHashSet(StringComparer.Ordinal);
        var permittedRoots=new HashSet<string>(StringComparer.Ordinal){"Realtime.FuturesTradeSessionBarSignal.Barrier","Realtime.FuturesTradePositionRealtime.EvaluateFuturesExitClock","Realtime.IronCondorTradePositionRealtime.IronCondorMonitoringInputsChanged"};
        if(config.RootRealtimeVerbs.Length!=permittedRoots.Count||!roots.SetEquals(permittedRoots))
            throw new InvalidDataException("Original replay requires exactly the three reviewed timer roots; zero observed events is permitted, arbitrary financial outputs are not.");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(config.MaximumReplaySeconds));
        await using var manager = new NatsConnectionManager();
        var client = await manager.GetClientAsync(natsUrl, stop.Token);
        var backend = new RecordedMarketDataBackend(baseline);
        await using var replies = await RecordedMarketDataNatsServer.StartAsync(client, scope, backend, stop.Token);
        var rawEnabled = bool.TryParse(isolatedMode.Settings.GetValueOrDefault("RawFuturesStorage:Enabled"), out var rawConfigured) && rawConfigured;
        var rawOptions = new RawFuturesStorageOptions { Enabled = rawEnabled, NatsUrl = natsUrl,
            Directory = Path.Combine(restoreRoot, "replay-raw-source"), Scope = isolatedMode.Settings.GetValueOrDefault("RawFuturesStorage:Scope") ?? "development" };
        var rawTransport = new RawFuturesStorageTransport(rawOptions, manager);
        using var sourceStatus = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        using var http = new HttpClient { BaseAddress = new Uri(apiUrl), Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Add("X-IFM-Replay-Token", token);
        http.DefaultRequestHeaders.Add("X-IFM-Session-Capture", token);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.GetFullPath(config.ApiAssembly)); start.ArgumentList.Add("--historical-session-replay=" + Path.GetFullPath(config.HostConfiguration));
        start.Environment["SessionCapture__AccessToken"] = token;
        start.Environment["SessionCapture__OwnerCheckpoints__Enabled"] = "true";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Test"; start.Environment["DOTNET_ENVIRONMENT"] = "Test";
        foreach (var key in start.Environment.Keys.Where(key => key.Contains("DATABENTO", StringComparison.OrdinalIgnoreCase)
                     || key.Contains("FMP_API_KEY", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        using var process = Process.Start(start) ?? throw new IOException("Replay API child did not start.");
        await using var stdout = File.Create(Path.Combine(output, "api.stdout.log"));
        await using var stderr = File.Create(Path.Combine(output, "api.stderr.log"));
        var copyOut = process.StandardOutput.BaseStream.CopyToAsync(stdout); var copyErr = process.StandardError.BaseStream.CopyToAsync(stderr);
        long ordinal = 0, injected = 0, regenerated = 0; var state = "Incomplete";
        var sourceHealth = rawEnabled ? rawTransport.RespondToHealthAsync(() => !stop.IsCancellationRequested, sourceStatus.Token) : Task.CompletedTask;
        try
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); startup.CancelAfter(TimeSpan.FromMinutes(3));
            while (true)
            {
                if (process.HasExited) throw new IOException("Replay API exited during startup.");
                try { using var status = await http.GetFromJsonAsync<JsonDocument>("/qualification/replay/status", startup.Token);
                    if (status!.RootElement.GetProperty("actorReady").GetBoolean() && status.RootElement.GetProperty("hubRunning").GetBoolean()) break; }
                catch (HttpRequestException) { }
                await Task.Delay(100, startup.Token);
            }
            var hello = await client.RequestAsync<NativeSourceCaptureHello, NativeSourceCaptureHelloReceipt>(scope + ".source-capture.hello", new(2, scope),
                requestSerializer: NatsMessagePackSerializer<NativeSourceCaptureHello>.Default,
                replySerializer: NatsMessagePackSerializer<NativeSourceCaptureHelloReceipt>.Default, cancellationToken: stop.Token);
            if (hello.Data is not { Accepted: true }) throw new InvalidDataException("Real source ingress did not negotiate protocol2.");
            var pacing = Stopwatch.StartNew();
            foreach (var entry in archive.ReadCommitted())
            {
                if(entry.Ordinal<=initialOrdinal)continue;
                stop.Token.ThrowIfCancellationRequested();
                var due = entry.RecordedAtUtc!.Value - first.Value - pacing.Elapsed;
                if (due > TimeSpan.Zero) await Task.Delay(due, stop.Token);
                var record = entry.Record;
                // Make the original completed external reply visible before advancing business-time workers to its barrier.
                if (record.Kind == "BackendRpc") backend.Accept(MarketDataHostConnection.Decode<RecordedMarketDataExchange>(record.OriginalWire.ToArray()));
                using var advanced = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(http,"/qualification/replay/clock", new { recordedArrivalUtc = entry.RecordedAtUtc, ordinal = ++ordinal }, stop.Token);
                advanced.EnsureSuccessStatusCode();
                if (record.Kind is "BackendRpc" or "MonitoringGenerationAccepted") continue;
                if(record.Kind=="EmulatorOwnerCheckpointRequest")
                {
                    var original=JsonSerializer.Deserialize<TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine.EmulatorOwnerCheckpointRequest>(record.OriginalWire.Span)
                        ??throw new InvalidDataException("Original owner checkpoint request is missing.");
                    if(original.BoundaryId==Guid.Empty||original.BoundaryId!=record.EventId||record.LogicalSourceKey!=original.BoundaryId.ToString("N")||record.Subject!="SessionCapture.EmulatorOwner.Checkpoint."+original.BoundaryId.ToString("N")||original.RequestedAtUtc.Kind!=DateTimeKind.Utc)
                        throw new InvalidDataException("Original owner checkpoint identity changed.");
                    using var administrativeDrain=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);administrativeDrain.CancelAfter(TimeSpan.FromSeconds(30));
                    await DrainAsync(http,rawTransport,rawOptions,administrativeDrain.Token).ConfigureAwait(false);
                    using var content=new ByteArrayContent(record.OriginalWire.ToArray());content.Headers.ContentType=new("application/json");
                    using var response=await http.PostAsync("/internal/session-capture/emulator/checkpoint/"+original.BoundaryId.ToString("N"),content,stop.Token);
                    if(!response.IsSuccessStatusCode)throw new InvalidDataException("Original non-economic owner checkpoint did not complete through its actual owner.");
                    injected++;continue;
                }
                if (record.Kind == "RawFuturesStorage")
                {
                    if (!rawEnabled || record.Subject != rawOptions.Subject) throw new InvalidDataException("Original raw storage route differs from restored configuration.");
                    await rawTransport.PublishAsync(record.OriginalWire.ToArray(), entry.OriginalWireSha256, stop.Token); injected++; continue;
                }
                if (record.Kind.StartsWith("NativeTransport/", StringComparison.Ordinal))
                {
                    var parts = record.Kind.Split('/'); if (parts.Length != 3 || parts[1] is not ("Futures" or "Options")) throw new InvalidDataException("Unknown original native transport.");
                    var subject = scope + ".source-capture." + (parts[2] == "Batch" ? "batch." : "") + parts[1];
                    var receipt = await client.RequestAsync<byte[], byte[]>(subject, record.OriginalWire.ToArray(),
                        headers: Headers(record), requestSerializer: new NatsByteArrayMessageSerializer(), replySerializer: new NatsByteArrayMessageSerializer(), cancellationToken: stop.Token);
                    if (receipt.Data is null) throw new InvalidDataException("Real source ingress omitted ownership receipt.");
                    if (parts[2] == "Batch")
                    {
                        var result = MessagePackSerializer.Deserialize<NativeSourceCaptureBatchReceipt>(receipt.Data);
                        var original = NativeSourceCaptureCodec.DecodeBatch(record.OriginalWire.ToArray());
                        if (result.Version != 2 || result.Sources.Length != original.Sources.Length) throw new InvalidDataException("Native group receipt cardinality changed.");
                        for (var member = 0; member < result.Sources.Length; member++) VerifyReceipt(result.Sources[member], original.Sources[member].Wire);
                    }
                    else if (parts[2] == "Single") VerifyReceipt(MessagePackSerializer.Deserialize<NativeSourceCaptureReceipt>(receipt.Data), record.OriginalWire.ToArray());
                    else throw new InvalidDataException("Unknown original source framing.");
                    injected++; continue;
                }
                if (record.Kind == "OptionCacheFrame") { await client.PublishAsync(scope + ".option-cache", record.OriginalWire.ToArray(), serializer: new NatsByteArrayMessageSerializer(), cancellationToken: stop.Token); injected++; continue; }
                if (record.Kind.StartsWith("NativeSource/", StringComparison.Ordinal)) { regenerated++; continue; } // members of the recorded physical transport
                if (record.Kind == "AccountObservation" && account == "RegenerateFromFileEmulator") { regenerated++; continue; }
                var split = record.Subject.Split('.', 4);
                if (record.Kind == "RealtimePublication" && split.Length == 4 && roots.Contains(string.Join('.', split.Take(3))))
                { await client.PublishAsync(record.Subject, record.OriginalWire.ToArray(), headers: Headers(record), serializer: new NatsByteArrayMessageSerializer(), cancellationToken: stop.Token); injected++; continue; }
                if (record.Kind == "AccountObservation" && account == "RecordedExternalInput")
                {
                    var js = await manager.GetJetStreamContextAsync(natsUrl, stop.Token);
                    var ack = await js.PublishAsync(record.Subject, record.OriginalWire.ToArray(), headers: Headers(record), serializer: new NatsByteArrayMessageSerializer(), cancellationToken: stop.Token);
                    ack.EnsureSuccess(); injected++; continue;
                }
                if (record.Kind != "RealtimePublication") throw new InvalidDataException("Unclassified original input kind: " + record.Kind);
                regenerated++; // domain outputs must be regenerated by the actual owners, never injected a second time
            }
            backend.CompleteTranscript();
            // Real durable ownership must drain. This does not replace subsequent exact owner-data reconciliation.
            while (true)
            {
                using var status = await http.GetFromJsonAsync<JsonDocument>("/qualification/replay/status", stop.Token);
                var s = status!.RootElement;
                if (s.GetProperty("failure").ValueKind != JsonValueKind.Null) throw new IOException("Replay transport reports an ownership failure.");
                var rawDrained = true;
                if (rawEnabled)
                {
                    var rawJs = await rawTransport.GetAsync(stop.Token);
                    var consumer = await rawJs.GetConsumerAsync(rawOptions.Stream, "raw_rows_v1", cancellationToken: stop.Token);
                    rawDrained = consumer.Info.NumPending == 0 && consumer.Info.NumAckPending == 0;
                }
                if (rawDrained && s.GetProperty("actorQueued").GetInt32() == 0 && s.GetProperty("actorProcessing").GetInt32() == 0 && s.GetProperty("pendingSources").GetInt32() == 0 && s.GetProperty("brokerPending").GetUInt64() == 0 && s.GetProperty("brokerUnacknowledged").GetInt64() == 0) break;
                await Task.Delay(20, stop.Token);
            }
            backend.VerifyComplete();
            state = "ExecutedAwaitingOwnerReconciliation";
        }
        finally
        {
            if (!process.HasExited)
            {
                using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                try
                {
                    while (true)
                    {
                        using var requested = await http.PostAsync("/qualification/replay/stop", null, shutdown.Token);
                        if (requested.StatusCode == System.Net.HttpStatusCode.Conflict) { await Task.Delay(20, shutdown.Token); continue; }
                        requested.EnsureSuccessStatusCode(); break;
                    }
                    await process.WaitForExitAsync(shutdown.Token);
                }
                catch (Exception error) when (error is HttpRequestException or OperationCanceledException) { state = "IncompleteChildRequiresInspection"; }
            }
            if (process.HasExited)
            {
                await Task.WhenAll(copyOut, copyErr);
                if (process.ExitCode != 0) state = "IncompleteChildFailed";
            }
            sourceStatus.Cancel();
            try { await sourceHealth; } catch (OperationCanceledException) { }
            catch (Exception) { state = "IncompleteOriginalInputTransport"; }
            await replies.DisposeAsync(); // settle all adapter requests before publishing any final transcript verdict
            try { backend.VerifyComplete(); }
            catch (InvalidDataException) { state = "IncompleteOriginalExternalInputs"; }
            if(config.UseCertifiedSharedArchive)
                try{PrepareArchive(config,restoreRoot);}catch(Exception error) when(error is IOException or InvalidDataException){state="IncompleteSharedOriginalInputChanged";}
            var result = new { Version = 1, State = state, Qualified = false, Records = ordinal, InjectedExternalInputs = injected,
                ExpectedRegeneratedOutputs = regenerated, backend.RecordedExchanges, OriginalExternalInputs = backend.Diagnostics, InitialArchiveOrdinal=initialOrdinal, InitialArchiveBoundarySha256=config.InitialArchiveBoundarySha256, NativeCheckpointSha256 = staged.Sha256,
                ApiProcessId = process.Id, ApiExited = process.HasExited, LiveActivationPerformed = false };
            await File.WriteAllTextAsync(Path.Combine(output, "replay.json"), JsonSerializer.Serialize(result, Json));
        }
        if (state != "ExecutedAwaitingOwnerReconciliation")
            throw new InvalidDataException("Historical replay did not complete; inspect the isolated replay report and child logs.");
    }

    static async Task DrainAsync(HttpClient http,RawFuturesStorageTransport transport,RawFuturesStorageOptions options,CancellationToken token)
    {
        while(true)
        {
            using var status=await http.GetFromJsonAsync<JsonDocument>("/qualification/replay/status",token);var s=status!.RootElement;
            if(s.GetProperty("failure").ValueKind!=JsonValueKind.Null)throw new IOException("Replay ownership is not healthy at its original administrative barrier.");
            var rawDrained=true;
            if(options.Enabled){var js=await transport.GetAsync(token);var consumer=await js.GetConsumerAsync(options.Stream,"raw_rows_v1",cancellationToken:token);rawDrained=consumer.Info.NumPending==0&&consumer.Info.NumAckPending==0;}
            var itiRequired=s.TryGetProperty("itiRequired",out var required)&&required.GetBoolean();
            var itiDrained=s.TryGetProperty("iti",out var iti)&&iti.ValueKind!=JsonValueKind.Null
                ?iti.GetProperty("drained").GetBoolean():!itiRequired;
            if(rawDrained&&itiDrained&&s.GetProperty("actorQueued").GetInt32()==0&&s.GetProperty("actorProcessing").GetInt32()==0&&s.GetProperty("pendingSources").GetInt32()==0&&s.GetProperty("brokerPending").GetUInt64()==0&&s.GetProperty("brokerUnacknowledged").GetInt64()==0)return;
            await Task.Delay(20,token);
        }
    }
    static NatsHeaders? Headers(RealtimeSessionArchiveRecord record)
    {
        var headers = global::TomasAI.IFM.Framework.Messaging.Nats.MessageOriginHeaders.Add(null, record.Origin);
        if (record.TraceParent is not null) { headers ??= new(); headers["traceparent"] = record.TraceParent; }
        if (record.TraceState is not null) { headers ??= new(); headers["tracestate"] = record.TraceState; }
        return headers;
    }
    static void VerifyReceipt(NativeSourceCaptureReceipt receipt, byte[] original)
    {
        var frame = NativeSourceCaptureCodec.DecodeFrame(original);
        if (!receipt.Captured || receipt.Worker != frame.WorkerInstanceId || receipt.Generation != frame.GenerationId
            || receipt.Sequence != frame.PublicationSequence || !string.Equals(receipt.WireSha256, Convert.ToHexStringLower(SHA256.HashData(original)), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Real source ownership receipt differs from original bytes/identity.");
    }
    static JsonDocument ReadJson(string path, long maximum)
    {
        if (new FileInfo(path).Length > maximum) throw new InvalidDataException("Replay metadata exceeds its finite bound.");
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }
    static void Verify(string path, string expected)
    { using var input = File.OpenRead(path); if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(input)), expected, StringComparison.Ordinal)) throw new InvalidDataException("Replay input changed: " + Path.GetFileName(path)); }
    static string Child(string root, string relative)
    {
        root = Path.GetFullPath(root); var path = Path.GetFullPath(Path.Combine(root, relative));
        if (Path.IsPathRooted(relative) || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Checkpoint path escapes root.");
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Replay paths cannot traverse filesystem links.");
        return path;
    }
    static void RequireLoopback(string value, string scheme)
    { var uri = new Uri(value); if (uri.Scheme != scheme || uri.Host != "127.0.0.1" || uri.UserInfo.Length > 0) throw new InvalidDataException("Only isolated loopback replay endpoints are supported."); }
    static void PrepareNativePrefixes(Configuration config, JsonElement certificate, string nativeRoot, string restoreRoot)
    {
        if (config.NativePrefixCopyManifest is null || config.NativePrefixCopyManifestSha256 is null)
            throw new InvalidDataException("Original source and raw prefixes require a full-SHA copy manifest.");
        Verify(config.NativePrefixCopyManifest, config.NativePrefixCopyManifestSha256);
        var prefixPath = Child(nativeRoot, certificate.GetProperty("SourcePrefixFile").GetString()!);
        var prefixSha = certificate.GetProperty("SourcePrefixSha256").GetString()!;
        Verify(prefixPath, prefixSha);
        using var prefixDocument = ReadJson(prefixPath, 1024 * 1024);
        using var copyDocument = ReadJson(config.NativePrefixCopyManifest, 16 * 1024 * 1024);
        var prefix = prefixDocument.RootElement; var copy = copyDocument.RootElement;
        if (copy.GetProperty("kind").GetString() != "NativeSourceImmutablePrefixes" || copy.GetProperty("prefixEvidenceSha256").GetString() != prefixSha
            || !prefix.GetProperty("CheckpointStateHeld").GetBoolean())
            throw new InvalidDataException("Copied native prefixes are not bound to the common frozen checkpoint.");
        var expected = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        void Add(JsonElement files, string kind)
        {
            foreach (var file in files.EnumerateArray())
            {
                var key = kind + "/" + file.GetProperty("RelativePath").GetString()!.Replace('\\', '/'); var length = file.GetProperty("Length").GetInt64();
                if (expected.TryGetValue(key, out var old) && old != length) throw new InvalidDataException("Checkpoint owners disagree about a frozen prefix.");
                expected[key] = length;
            }
        }
        Add(prefix.GetProperty("Files"), "source");
        foreach (var raw in prefix.GetProperty("RawSources").EnumerateArray()) Add(raw.GetProperty("Files"), "raw");
        var inputRoot = Path.GetDirectoryName(Path.GetFullPath(config.NativePrefixCopyManifest))!;
        var scope = certificate.GetProperty("Scope").GetString()!;
        if (scope.Any(value => !(char.IsAsciiLetterOrDigit(value) || value == '-'))) throw new InvalidDataException("Invalid original native source scope.");
        long total = 0; var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in copy.GetProperty("files").EnumerateArray())
        {
            var kind = file.GetProperty("ownerKind").GetString(); var relative = file.GetProperty("restoreRelativePath").GetString()!;
            var key = kind + "/" + relative.Replace('\\', '/'); var length = file.GetProperty("bytes").GetInt64();
            if (!seen.Add(key) || !expected.TryGetValue(key, out var required) || required != length || length < 0)
                throw new InvalidDataException("Native prefix copy has changed, duplicate or extra files.");
            total = checked(total + length); if (total > 16L * 1024 * 1024 * 1024) throw new InvalidDataException("Native copy exceeds replay budget.");
            var source = Child(inputRoot, file.GetProperty("relativePath").GetString()!);
            var targetRoot = kind == "source" ? Path.Combine(restoreRoot, "native-sources", scope) : Path.Combine(restoreRoot, "native-raw");
            var target = Child(targetRoot, relative); var sha = file.GetProperty("sha256").GetString()!;
            if (new FileInfo(source).Length != length) throw new InvalidDataException("Native prefix was truncated.");
            Verify(source, sha); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target)) File.Copy(source, target, false);
            if (new FileInfo(target).Length != length) throw new InvalidDataException("Restored native prefix has the wrong length.");
            Verify(target, sha);
        }
        if (seen.Count != expected.Count) throw new InvalidDataException("Native prefix copy is incomplete.");
    }
    static RealtimeSessionArchiveBoundary ReadInitialBoundary(Configuration config,JsonElement host,string restoreRoot)
    {
        if(config.InitialArchiveBoundary is null||config.InitialArchiveBoundarySha256 is null)throw new InvalidDataException("An exact original owner-bound archive floor is required; a caller ordinal or timestamp is not sufficient.");
        var path=Path.GetFullPath(config.InitialArchiveBoundary);_=Child(restoreRoot,Path.GetRelativePath(restoreRoot,path));Verify(path,config.InitialArchiveBoundarySha256);
        using var certificate=ReadJson(host.GetProperty("restoreCertificatePath").GetString()!,4*1024*1024);
        var certified=certificate.RootElement.GetProperty("ownerFiles").EnumerateArray().Where(file=>file.GetProperty("owner").GetString()=="session-archive-boundary")
            .ToDictionary(file=>Path.GetFullPath(file.GetProperty("path").GetString()!),file=>file.GetProperty("sha256").GetString()!,StringComparer.OrdinalIgnoreCase);
        if(!certified.TryGetValue(path,out var actualSha)||actualSha!=config.InitialArchiveBoundarySha256)throw new InvalidDataException("Initial archive floor is not an original hash-certified restored snapshot owner file.");
        using var document=ReadJson(path,4*1024*1024);var boundary=document.RootElement.Deserialize<RealtimeSessionArchiveBoundary>(Json)??throw new InvalidDataException("Initial archive boundary is absent.");
        var evidence=Child(Path.GetDirectoryName(path)!,"initial-archive-prefix.json");
        if(!certified.TryGetValue(evidence,out var evidenceSha)||evidenceSha!=boundary.PrefixEvidenceSha256)throw new InvalidDataException("Initial archive prefix evidence is not certified with its owner boundary.");Verify(evidence,evidenceSha);
        foreach(var file in boundary.Files){var prefix=Child(Path.Combine(Path.GetDirectoryName(path)!,"initial-archive"),file.RelativePath);if(!certified.TryGetValue(prefix,out var sha)||!string.Equals(sha,file.Sha256,StringComparison.OrdinalIgnoreCase)||new FileInfo(prefix).Length!=file.Length)throw new InvalidDataException("Initial archive prefix owner file is missing or changed.");Verify(prefix,sha);}
        return boundary;
    }
    static void PrepareArchive(Configuration config,string restoreRoot)
        =>CertifiedReplayArchiveInputs.Prepare(config.WorkingArchiveDirectory,config.ArchiveSessionId,config.ArchiveCopyManifest??throw new InvalidDataException("Original archive manifest required."),
            config.ArchiveCopyManifestSha256??throw new InvalidDataException("Original archive hash required."),restoreRoot,config.UseCertifiedSharedArchive);
}
