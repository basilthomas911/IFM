using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class FuturesItiSignalGeneratedCompleteTests
{
    [Fact]
    public async Task DailyCompletion_RequestsWeeklyAndMonthlyFromPersistedSignal()
    {
        var source = CreateCompletion(TimeFrameType.Daily);
        var context = CreateSuccessfulContext();

        var result = await FuturesItiSignalGeneratedComplete.GenerateDerivedPeriodsAsync(
            source,
            context,
            Substitute.For<ILogger>());

        result.Should().BeTrue();
        await context.Received(1).RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
            Arg.Is<GenerateFuturesItiSignalCommand>(command =>
                command.TimePeriod == TimeFrameType.Weekly
                && command.ContractId == source.FuturesItiSignal!.ContractId
                && command.ValueDate == source.FuturesItiSignal.ValueDate
                && command.Timestamp == source.FuturesItiSignal.IntrinsicTime
                && command.FuturesPrice == source.FuturesItiSignal.IntrinsicPrice
                && command.VixFuturesPrice == source.VixFuturesPrice
                && command.TimeFrameStartValueDate == new DateOnly(2026, 8, 10)));
        await context.Received(1).RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
            Arg.Is<GenerateFuturesItiSignalCommand>(command =>
                command.TimePeriod == TimeFrameType.Monthly
                && command.TimeFrameStartValueDate == new DateOnly(2026, 8, 1)));
    }

    [Theory]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public async Task LongerPeriodCompletion_DoesNotRecursivelyGenerateCommands(TimeFrameType period)
    {
        var context = CreateSuccessfulContext();

        var result = await FuturesItiSignalGeneratedComplete.GenerateDerivedPeriodsAsync(
            CreateCompletion(period), context, Substitute.For<ILogger>());

        result.Should().BeTrue();
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
    }

    [Fact]
    public async Task DailyCompletion_OneRejectedChildStillAttemptsBothAndReportsFailure()
    {
        var context = Substitute.For<IEventActorContext<FuturesItiSignalEventActor>>();
        context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
                Arg.Any<GenerateFuturesItiSignalCommand>())
            .Returns(call => call.Arg<GenerateFuturesItiSignalCommand>().TimePeriod == TimeFrameType.Weekly
                ? new ServiceFailed<GuidResult>(20011, "weekly rejected")
                : new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));

        var result = await FuturesItiSignalGeneratedComplete.GenerateDerivedPeriodsAsync(
            CreateCompletion(TimeFrameType.Daily), context, Substitute.For<ILogger>());

        result.Should().BeFalse();
        await context.Received(2).RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
            Arg.Any<GenerateFuturesItiSignalCommand>());
    }

    [Fact]
    public async Task DailyCompletion_RedeliveryUsesStableDistinctChildCommandIds()
    {
        var source = CreateCompletion(TimeFrameType.Daily);
        var first = CreateSuccessfulContext();
        var second = CreateSuccessfulContext();

        await FuturesItiSignalGeneratedComplete.GenerateDerivedPeriodsAsync(
            source, first, Substitute.For<ILogger>());
        await FuturesItiSignalGeneratedComplete.GenerateDerivedPeriodsAsync(
            source, second, Substitute.For<ILogger>());

        var firstCommands = GetCommands(first);
        var secondCommands = GetCommands(second);
        firstCommands.Should().HaveCount(2);
        firstCommands.Select(command => command.CommandId)
            .Should().Equal(secondCommands.Select(command => command.CommandId));
        firstCommands.Select(command => command.CommandId).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public async Task GenerateCompletion_StartsOneStrategyWorkflowWithPersistedTrigger(
        TimeFrameType period)
    {
        var completed = CreateCompletion(period);
        var context = CreateSuccessfulContext();

        await FuturesItiSignalGeneratedComplete.StartStrategyWorkflowAsync(completed, context);

        await context.Received(1).SendAsync<ExecuteIntrinsicTimeStrategyWorkflowCommand,
            IntrinsicTimeStrategyWorkflowEntityId>(
            Arg.Is<ExecuteIntrinsicTimeStrategyWorkflowCommand>(command =>
                command.CommandId == completed.Id
                && command.TriggerEventId == completed.Id
                && command.EntityId.ItiSignalEntityId == completed.EntityId
                && command.TriggerEvent.FuturesItiSignal == completed.FuturesItiSignal
                && command.TriggerEvent.EntityId.TimePeriod == period),
            Arg.Is<IntrinsicTimeStrategyWorkflowEntityId>(entityId =>
                entityId.ItiSignalEntityId == completed.EntityId));
    }

    [Fact]
    public async Task DailyCompletion_MissingSignalSnapshotIsRejectedWithoutChildCommands()
    {
        var source = CreateCompletion(TimeFrameType.Daily) with { FuturesItiSignal = null };
        var context = CreateSuccessfulContext();
        var status = Substitute.For<IStatusConsoleWriter>();

        var result = await source.ExecuteAsync(context, status, Substitute.For<ILogger>());

        result.Should().BeFalse();
        await context.DidNotReceiveWithAnyArgs()
            .RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(default!);
        await status.Received(1).WriteConsoleAsync(
            Arg.Any<LogSourceType>(),
            FuturesItiSignalGeneratedCompleteEvent.ErrorCode,
            Arg.Is<string>(message => message.Contains("persisted signal snapshot", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(TimeFrameType.Daily, "2026-08-14")]
    [InlineData(TimeFrameType.Weekly, "2026-08-10")]
    [InlineData(TimeFrameType.Monthly, "2026-08-01")]
    public void CalendarBucket_UsesExpectedBoundary(TimeFrameType period, string expected)
    {
        FuturesItiSignalTimeFrame.GetCalendarBucketStart(new DateOnly(2026, 8, 14), period)
            .Should().Be(DateOnly.Parse(expected));
    }

    [Fact]
    public void CalendarBucket_RejectsUnsupportedTimeFrame()
    {
        FluentActions.Invoking(() => FuturesItiSignalTimeFrame.GetCalendarBucketStart(
                new DateOnly(2026, 8, 14), TimeFrameType.OneMinute))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FuturesItiSignalDomain_HasThinRealtimeIngressWithoutRetiredStateOrProjector()
    {
        var names = MarketDataAnalyticsActorAssembly.Current.GetTypes()
            .Where(type => type.Namespace?.Contains("FuturesItiSignal", StringComparison.Ordinal) == true)
            .Select(type => type.Name)
            .ToArray();

        names.Should().Contain("FuturesItiSignalRealtimeActor");
        names.Should().Contain("FuturesMarketPriceUpdated");
        names.Should().NotContain(name =>
            name == "FuturesItiSignalRealtimeState"
            || name == "FuturesItiSignalStreamOwnership"
            || name == "FuturesItiSignalRealtimeProjector");
    }

    static GenerateFuturesItiSignalCommand[] GetCommands(
        IEventActorContext<FuturesItiSignalEventActor> context) =>
        context.ReceivedCalls()
            .Select(call => call.GetArguments().OfType<GenerateFuturesItiSignalCommand>().SingleOrDefault())
            .Where(command => command is not null)
            .Cast<GenerateFuturesItiSignalCommand>()
            .OrderBy(command => command.TimePeriod)
            .ToArray();

    static IEventActorContext<FuturesItiSignalEventActor> CreateSuccessfulContext()
    {
        var context = Substitute.For<IEventActorContext<FuturesItiSignalEventActor>>();
        context.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(
                Arg.Any<GenerateFuturesItiSignalCommand>())
            .Returns(new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid())));
        return context;
    }

    internal static FuturesItiSignalGeneratedCompleteEvent CreateCompletion(TimeFrameType period)
    {
        var source = SampleData.CreateItiSignalGeneratedCompleteEvent();
        var valueDate = new DateOnly(2026, 8, 14);
        var frameStart = FuturesItiSignalTimeFrame.GetCalendarBucketStart(valueDate, period);
        var entityId = new FuturesItiSignalEntityId(SampleData.ContractId, frameStart, period);
        return source with
        {
            Subject = new ActorSubject(
                ActorType.Event,
                FuturesItiSignalGeneratedCompleteEvent.Actor,
                FuturesItiSignalGeneratedCompleteEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            FuturesItiSignal = source.FuturesItiSignal! with
            {
                ContractId = SampleData.ContractId,
                ValueDate = valueDate,
                TimePeriod = period,
                TimeFrameStartValueDate = frameStart
            },
            VixFuturesPrice = 22.75,
            DeriveLongerPeriods = false
        };
    }
}
