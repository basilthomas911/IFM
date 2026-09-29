using System;
using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorInformationLoggingPolicyTests
{
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
