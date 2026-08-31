using FluentAssertions;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Operations;

public sealed class PortfolioOperationalGuardTests
{
    [Fact]
    [Trait("Gate", "PF-30")]
    [Trait("Category", "BDD")]
    public void Authorized_personas_can_use_only_their_bounded_journeys()
    {
        var guard = new PortfolioOperationalGuard(new());
        var admin = Request(PortfolioAccessContext.Administrator("alice"));
        var workflow = Request(PortfolioAccessContext.Workflow("workflow-7"));
        var reader = Request(PortfolioAccessContext.Reader("auditor"));

        guard.Demand(PortfolioOperation.AdministerPortfolio, admin, true).Principal.Should().Be("alice");
        guard.Demand(PortfolioOperation.RecordRiskResult, workflow, true).Principal.Should().Be("workflow-7");
        guard.Demand(PortfolioOperation.Read, reader, false).Principal.Should().Be("auditor");
        guard.Invoking(x => x.Demand(PortfolioOperation.AdministerFund, reader, true))
            .Should().Throw<PortfolioAuthorizationException>();
    }

    [Fact]
    [Trait("Gate", "PF-30")]
    [Trait("Category", "BDD")]
    public void Anonymous_and_deferred_execution_authority_are_prohibited()
    {
        var guard = new PortfolioOperationalGuard(new());
        guard.Invoking(x => x.Demand(PortfolioOperation.Read, Request(new()), false))
            .Should().Throw<PortfolioAuthorizationException>();
        Enum.GetNames<PortfolioOperation>().Should().NotContain(name => name.Contains("Execution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Gate", "PF-30")]
    [Trait("Category", "Portfolio")]
    public void Rollback_switches_independently_disable_mutations_and_queries()
    {
        var mutationOff = new PortfolioOperationalGuard(new() { MutationsEnabled = false });
        var queryOff = new PortfolioOperationalGuard(new() { QueriesEnabled = false });

        mutationOff.Invoking(x => x.Demand(PortfolioOperation.AdministerPortfolio,
            Request(PortfolioAccessContext.Administrator("admin")), true)).Should().Throw<PortfolioOperationalException>();
        mutationOff.Demand(PortfolioOperation.Read, Request(PortfolioAccessContext.Reader("reader")), false).Should().NotBeNull();
        queryOff.Invoking(x => x.Demand(PortfolioOperation.Read,
            Request(PortfolioAccessContext.Reader("reader")), false)).Should().Throw<PortfolioOperationalException>();
        queryOff.Demand(PortfolioOperation.AdministerPortfolio,
            Request(PortfolioAccessContext.Administrator("admin")), true).Should().NotBeNull();
    }

    [Fact]
    [Trait("Gate", "PF-30")]
    public void Caller_scope_is_async_local_and_restores_the_previous_principal()
    {
        PortfolioAccessScope.Current.Should().BeNull();
        using (PortfolioAccessScope.Push(PortfolioAccessContext.Reader("outer")))
        {
            PortfolioAccessScope.Current!.Principal.Should().Be("outer");
            using (PortfolioAccessScope.Push(PortfolioAccessContext.Administrator("inner")))
                PortfolioAccessScope.Current!.Principal.Should().Be("inner");
            PortfolioAccessScope.Current!.Principal.Should().Be("outer");
        }
        PortfolioAccessScope.Current.Should().BeNull();
    }

    [Fact]
    [Trait("Gate", "PF-30")]
    public void Invalid_enabled_configuration_is_rejected_while_full_disable_is_a_valid_rollback_mode()
    {
        var invalid = () => new PortfolioOperationalGuard(new() { Enabled = true, QueriesEnabled = false, MutationsEnabled = false });
        invalid.Should().Throw<InvalidOperationException>();
        var disabled = new PortfolioOperationalGuard(new() { Enabled = false, QueriesEnabled = false, MutationsEnabled = false });
        disabled.Options.Enabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Gate", "PF-30")]
    public void Trace_contains_required_bounded_correlation_fields_and_never_records_the_principal()
    {
        Activity? captured = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PortfolioTelemetry.InstrumentationName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => captured = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var request = Request(PortfolioAccessContext.Administrator("secret-principal"));

        using (PortfolioTelemetry.StartRequest("command", "CreatePortfolio", request)) { }

        captured.Should().NotBeNull();
        captured!.Tags.Should().Contain(x => x.Key == "portfolio.operation" && x.Value == "CreatePortfolio");
        captured.Tags.Should().Contain(x => x.Key == "correlation.id" && x.Value == request.CorrelationId.ToString("N"));
        captured.Tags.Should().NotContain(x => x.Value != null && x.Value.Contains("secret-principal", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Gate", "PF-30")]
    public void Metric_capture_uses_only_bounded_operation_and_outcome_dimensions()
    {
        var captures = new List<(string Name, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == PortfolioTelemetry.InstrumentationName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            captures.Add((instrument.Name, tags.ToArray())));
        listener.Start();

        new PortfolioOperationalGuard(new()).Demand(PortfolioOperation.Read,
            Request(PortfolioAccessContext.Reader("must-not-be-a-label")), false);

        var capture = captures.Should().ContainSingle(x => x.Name == "portfolio.authorization.checks").Subject;
        capture.Tags.Select(x => x.Key).Should().BeEquivalentTo("portfolio.operation", "portfolio.outcome");
        capture.Tags.Select(x => x.Value?.ToString()).Should().NotContain("must-not-be-a-label");
    }

    static IPortfolioRequestMetadata Request(PortfolioAccessContext access) => new RequestMetadata
    {
        CorrelationId = Guid.NewGuid(), RequestedOnUtc = DateTime.UtcNow, Access = access,
    };

    sealed record RequestMetadata : IPortfolioRequestMetadata
    {
        public Guid CorrelationId { get; init; }
        public DateTime RequestedOnUtc { get; init; }
        public PortfolioAccessContext Access { get; init; } = new();
    }
}
