using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;
using Cache = TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class MarketDataReadMirrorTests
{
    sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => At; }
    [Fact]
    public async Task Fragmented_publication_rejects_missing_or_corrupted_frames_and_replays_without_partial_visibility()
    {
        var snapshot = await OptionChainCacheTests.Snapshot();
        // Repeated rows enlarge this transport fixture; this test does not qualify a trade universe.
        snapshot = snapshot with { Instruments = System.Collections.Immutable.ImmutableArray.CreateRange(Enumerable.Repeat(snapshot.Instruments[0], 500)) };
        var publication = new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainPublication(snapshot, new(2026,9,8), "fixture", 1, "scope");
        var frames = TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainPublicationFrame.Encode(publication);
        Assert.True(frames.Length > 1);
        Assert.All(frames, frame => Assert.True(frame.Bytes.Length <= 64 * 1024));
        var assembler = new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainPublicationAssembler();
        Assert.Null(assembler.Accept(frames[1]));
        Assert.Null(assembler.Accept(frames[0]));
        Assert.Null(assembler.Accept(frames[^1] with { Digest = new string('0', 64) }));
        TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainPublication? complete = null;
        foreach (var frame in frames) complete = assembler.Accept(frame);
        Assert.NotNull(complete);
        Assert.Equal(500, complete.Snapshot.Instruments.Length);
        var corrupted = frames.Select(frame => frame with { Bytes = frame.Bytes.ToArray() }).ToArray();
        corrupted[^1].Bytes[^1] ^= 1;
        foreach (var frame in corrupted) Assert.Null(assembler.Accept(frame));
    }
    [Fact]
    public async Task Prepared_option_cache_survives_wire_bootstrap_and_is_fenced_on_terminal_failure()
    {
        var snapshot = await OptionChainCacheTests.Snapshot();
        using var cache = new Cache(new Clock());
        var mirror = new MarketDataReadMirror(cache);
        var host = Guid.NewGuid();
        var healthy = new MarketDataDatasetStatus("Options", "GLBX.MDP3", Guid.NewGuid(), Generation, 100, true, true, "Healthy", null, new(2026,9,8));
        var boot = new MarketDataReadSnapshot(new(true, new(2026,9,8), [healthy], host, 1), [], [], [],
            [new(snapshot, new(2026,9,8), "reviewed-policy/v1", 7, "fixture-scope")]);
        Assert.True(mirror.Apply(MarketDataHostConnection.Decode<MarketDataReadSnapshot>(MarketDataHostConnection.Encode(boot))));
        Assert.True(cache.GetStatus(new("fixture-scope", Generation)).Published);
        var failed = healthy with { Lifecycle = "StoppedError", Healthy = false, Running = false, Error = "Injected timeout", ChangedAtUtc = At };
        Assert.True(mirror.Apply(boot with { Status = new(true, new(2026,9,8), [failed], host, 2), OptionChains = [] }));
        Assert.False(cache.GetStatus(new("fixture-scope", Generation)).Published);
        Assert.Equal("Injected timeout", mirror.GetRuntimeStatus().OptionFeedError);
        Assert.True(mirror.GetRuntimeStatus().OptionCleanupConfirmed);
        Assert.False(mirror.Apply(boot));
        mirror.AcceptOption(boot.OptionChains![0]);
        Assert.False(cache.GetStatus(new("fixture-scope", Generation)).Published);
    }
    [Fact]
    public void Old_Host_responses_cannot_reopen_a_retired_backend_after_reconnect()
    {
        using var cache = new Cache(); var mirror = new MarketDataReadMirror(cache);
        var old = new MarketDataReadSnapshot(new(true, null, [], Guid.NewGuid(), 10), [], [], []);
        Assert.True(mirror.Apply(old));
        mirror.Disconnect();
        Assert.True(mirror.Apply(old with { Status = new(true, null, [], Guid.NewGuid(), 1) }));
        Assert.False(mirror.Apply(old with { Status = old.Status with { Revision = 11 } }));
    }
}
