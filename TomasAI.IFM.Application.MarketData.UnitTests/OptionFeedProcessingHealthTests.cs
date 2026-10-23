using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionFeedProcessingHealthTests
{
    static readonly DateOnly Date = new(2026, 10, 23);
    [Theory]
    [InlineData("LeaseEnded")]
    [InlineData("UnexpectedProviderCode")]
    public void Unknown_or_terminal_failure_is_immediately_unhealthy_and_not_cleared_by_one_success(string code)
    {
        var health = new OptionFeedProcessingHealth(); var generation = Guid.NewGuid();
        health.Report(Date, generation, "lease", new OptionFeedProcessingException(code));
        Assert.Equal(code, health.Read(Date, generation)!.Code);
        health.Succeeded(Date, generation, "lease"); Assert.NotNull(health.Read(Date, generation));
        Assert.Null(health.Read(Date, Guid.NewGuid())); Assert.Null(health.Read(Date.AddDays(1), generation));
    }
    [Fact]
    public void Known_transient_has_finite_attempts_and_unknown_timeout_is_not_assumed_transient()
    {
        var health = new OptionFeedProcessingHealth(); var generation = Guid.NewGuid();
        for (var i = 0; i < 2; i++) health.Report(Date, generation, "lease", new OptionFeedProcessingException("UnderlyingQuoteUnavailable"));
        Assert.Null(health.Read(Date, generation));
        health.Report(Date, generation, "lease", new OptionFeedProcessingException("UnderlyingQuoteUnavailable"));
        Assert.NotNull(health.Read(Date, generation));
        health.Report(Date, generation, "timeout", new TimeoutException("expired"));
        Assert.NotNull(health.Read(Date, generation));
    }
    [Fact]
    public void Intentional_cancellation_does_not_mark_unhealthy_but_unowned_cancellation_does()
    {
        var health = new OptionFeedProcessingHealth(); var generation = Guid.NewGuid();
        using var stop = new CancellationTokenSource(); stop.Cancel();
        health.Report(Date, generation, "lease", new OperationCanceledException(), stop.Token);
        Assert.Null(health.Read(Date, generation));
        health.Report(Date, generation, "lease", new OperationCanceledException());
        Assert.NotNull(health.Read(Date, generation));
    }
    [Fact]
    public void Recovery_cannot_clear_a_failure_that_arrived_after_its_evidence_boundary()
    {
        var health = new OptionFeedProcessingHealth(); var generation = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        health.Report(Date, generation, "lease", new InvalidDataException("bad frame"));
        health.Recovered(Date, generation, before); Assert.NotNull(health.Read(Date, generation));
        health.Recovered(Date, generation, DateTimeOffset.UtcNow.AddSeconds(1)); Assert.Null(health.Read(Date, generation));
    }
    [Fact]
    public void Host_failure_and_live_intent_survive_wire_transport_and_reach_API_read_status()
    {
        var generation = Guid.NewGuid(); var host = Guid.NewGuid();
        var failure = new OptionFeedProcessingFailure(Date, generation, DateTimeOffset.UtcNow, "Renewal", "LeaseEnded", "Original error detail");
        var status = new TomasAI.IFM.Application.MarketData.Contracts.MarketDataBackendStatus(true, Date,
            [new("Options", "GLBX.MDP3", Guid.NewGuid(), generation, 123, true, false, "Healthy", failure.Detail, Date)],
            host, 1, OptionProcessingFailure: failure, EnabledOptionTrades: ["101.701.1901.1501"]);
        var snapshot = new TomasAI.IFM.Application.MarketData.Contracts.MarketDataReadSnapshot(status, [], [], []);
        var encoded = TomasAI.IFM.Application.MarketData.Databento.Workers.MarketDataHostConnection.Encode(snapshot);
        var decoded = TomasAI.IFM.Application.MarketData.Databento.Workers.MarketDataHostConnection.Decode<TomasAI.IFM.Application.MarketData.Contracts.MarketDataReadSnapshot>(encoded);
        var mirror = new TomasAI.IFM.Application.MarketData.Remote.MarketDataReadMirror(new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache());
        Assert.True(mirror.Apply(decoded)); var runtime = mirror.GetRuntimeStatus();
        Assert.Contains("LeaseEnded", runtime.OptionProcessingError); Assert.Contains("Original error detail", runtime.OptionProcessingError);
        Assert.Equal(failure.ObservedAtUtc, runtime.OptionProcessingFailureUtc); Assert.Equal(generation, runtime.OptionGenerationId);
        Assert.Equal(["101.701.1901.1501"], runtime.EnabledOptionTrades);
    }

    [Theory]
    [InlineData(1, "AUTH_FAILED")]
    [InlineData(2, "API_KEY_DEACTIVATED")]
    [InlineData(3, "CONNECTION_LIMIT_EXCEEDED")]
    [InlineData(4, "SYMBOL_RESOLUTION_FAILED")]
    [InlineData(5, "INVALID_SUBSCRIPTION")]
    [InlineData(6, "INTERNAL_ERROR")]
    [InlineData(7, "SKIPPED_RECORDS_AFTER_SLOW_READING")]
    [InlineData(8, "REPLAY_DATA_AGED_OUT")]
    [InlineData(255, "UNSET")]
    [InlineData(99, "UNKNOWN")]
    public void Provider_codes_and_full_messages_remain_distinct(int code, string expected)
    {
        var value = new DatabentoProviderDiagnostic(1, code, true, true, new string('x', 1024));
        Assert.Equal(expected, value.CodeName); Assert.Equal(1024, value.Message.Length);
        Assert.Equal("SLOW_READER_WARNING", new DatabentoProviderDiagnostic(2, 2, false, true, "warning").CodeName);
    }
}
