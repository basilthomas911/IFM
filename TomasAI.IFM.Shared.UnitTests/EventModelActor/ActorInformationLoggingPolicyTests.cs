using System;
using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorInformationLoggingPolicyTests
{
    [Fact]
    public void Sampling_is_exact_under_concurrency_and_does_not_enable_explicitly_suppressed_routes()
    {
        var policy = new ActorInformationLoggingOptions { SuccessfulQuerySampleEvery = 10 }.Compile();
        var id = new ActorThreadId(ActorType.Query, "Performance", "ES");
        var selected = 0;
        Parallel.For(0, 10000, _ =>
        {
            if (policy.SelectRoutineInformation(id, "Read")) Interlocked.Increment(ref selected);
            else policy.RecordSuppressedSuccess(id);
        });
        selected.Should().Be(1000);
        policy.SampledOutCount.Should().Be(9000);
        policy.SelectRoutineInformation(new(ActorType.Realtime, "Performance", "ES"), "Read").Should().BeFalse();
        policy.SelectRoutineInformation(new(ActorType.Command, "Performance", "ES"), "Change").Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Sampling_intervals_must_be_positive(int every)
        => new ActorInformationLoggingOptions { SuccessfulQuerySampleEvery = every }.Invoking(options => options.Compile())
            .Should().Throw<InvalidOperationException>();

    [Fact]
    public void Slow_override_is_opt_in_and_does_not_override_explicit_suppression()
    {
        var policy = new ActorInformationLoggingOptions { SuccessfulQuerySampleEvery = 100, SlowOperationMilliseconds = 1 }.Compile();
        var earlier = System.Diagnostics.Stopwatch.GetTimestamp() - System.Diagnostics.Stopwatch.Frequency;
        policy.CapturesSlowInformation(new(ActorType.Query, "Read", "ES"), "Read", earlier).Should().BeTrue();
        policy.CapturesSlowInformation(new(ActorType.Realtime, "Read", "ES"), "Read", earlier).Should().BeFalse();
        ActorInformationLoggingPolicy.Default.CapturesSlowInformation(new(ActorType.Query, "Read", "ES"), "Read", earlier).Should().BeFalse();
        new ActorInformationLoggingOptions { SlowOperationMilliseconds = double.NaN }.Invoking(options => options.Compile())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Compile_MatchesOnlyExactActorTypeNameAndVerb()
    {
        var policy = new ActorInformationLoggingOptions
        {
            SuppressedRoutes =
            [
                new() { ActorType = ActorType.Command, Name = "TickAggregation", Verb = "Rebuild" }
            ]
        }.Compile();

        policy.SuppressesRoutineInformation(
            new(ActorType.Command, "TickAggregation", "ES"), "Rebuild").Should().BeTrue();
        policy.SuppressesRoutineInformation(
            new(ActorType.Command, "TickAggregation", "NQ"), "Rebuild").Should().BeTrue();
        policy.SuppressesRoutineInformation(
            new(ActorType.Command, "TickAggregation", "ES"), "Read").Should().BeFalse();
        policy.SuppressesRoutineInformation(
            new(ActorType.Query, "TickAggregation", "ES"), "Rebuild").Should().BeFalse();
    }

    [Fact]
    public void Compile_DefaultSuppressesEventAndRealtimeButLogsCommandAndQuery()
    {
        var policy = new ActorInformationLoggingOptions().Compile();

        policy.SuppressesRoutineInformation(new(ActorType.Event, "Any", "1"), "Changed").Should().BeTrue();
        policy.SuppressesRoutineInformation(new(ActorType.Realtime, "Any", "1"), "Updated").Should().BeTrue();
        policy.SuppressesRoutineInformation(new(ActorType.Command, "Any", "1"), "Execute").Should().BeFalse();
        policy.SuppressesRoutineInformation(new(ActorType.Query, "Any", "1"), "Get").Should().BeFalse();
    }

    [Fact]
    public void Compile_RejectsDuplicateAndIncompleteRoutes()
    {
        var duplicate = new ActorInformationLoggingOptions
        {
            SuppressedRoutes =
            [
                new() { ActorType = ActorType.Realtime, Name = "Feed", Verb = "Updated" },
                new() { ActorType = ActorType.Realtime, Name = "Feed", Verb = "Updated" }
            ]
        };
        var incomplete = new ActorInformationLoggingOptions
        {
            SuppressedRoutes = [new() { ActorType = ActorType.Realtime, Name = "Feed" }]
        };

        duplicate.Invoking(options => options.Compile()).Should().Throw<InvalidOperationException>();
        incomplete.Invoking(options => options.Compile()).Should().Throw<InvalidOperationException>();
    }
}
