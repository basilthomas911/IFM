using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Actor;
using TomasAI.IFM.Domain.MarketData.DownloadLog;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.DownloadLog;

[Trait("Category", "BDD")]
public sealed class DownloadLogBddTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Given_a_terminal_outcome_when_forwarded_then_the_original_attempt_and_measurements_are_preserved(bool failed)
    {
        var outcome = DownloadLogContractTests.Outcome(MarketDataDownloadDataset.TreasuryCurve);
        if (failed) outcome = outcome with { Status = MarketDataDownloadStatus.Failed, PersistedRecordCount = null, ErrorCode = "WriteFailed", ErrorMessage = "Write was not confirmed." };
        var context = Substitute.For<IEventActorContext>();
        InsertMarketDataDownloadLogCommand? captured = null;
        context.RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId>(Arg.Do<InsertMarketDataDownloadLogCommand>(c => captured = c))
            .Returns(new ServiceOk<GuidResult>(new GuidResult(MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId))));
        if (failed)
            await new YieldCurveRatesImportedFailEvent { ImportDate = outcome.ValueDate.ToDateTime(TimeOnly.MinValue), Id = outcome.SourceTerminalEventId, CommandId = outcome.ImportCommandId, DownloadOutcome = outcome }
                .ExecuteAsync(context, NullLogger<YieldCurveRateEventActor>.Instance);
        else
            await new YieldCurveRatesImportedCompleteEvent { ImportDate = outcome.ValueDate.ToDateTime(TimeOnly.MinValue), Id = outcome.SourceTerminalEventId, CommandId = outcome.ImportCommandId, DownloadOutcome = outcome }
                .ExecuteAsync(context, NullLogger<YieldCurveRateEventActor>.Instance);
        Assert.Equal(outcome, captured!.Outcome); captured.Validate();
    }

    [Fact] public async Task Given_logging_rejection_when_forwarding_then_recovery_keeps_the_original_outcome()
    {
        var o = DownloadLogContractTests.Outcome(MarketDataDownloadDataset.TreasuryCurve); var context = Substitute.For<IEventActorContext>();
        context.RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId>(Arg.Any<InsertMarketDataDownloadLogCommand>())
            .Returns(new ServiceFailed<GuidResult>(503, "Unavailable"));
        var terminal = new YieldCurveRatesImportedCompleteEvent { ImportDate = o.ValueDate.ToDateTime(TimeOnly.MinValue), Id = o.SourceTerminalEventId, CommandId = o.ImportCommandId, DownloadOutcome = o };
        var error = await Assert.ThrowsAsync<DownloadLogDeliveryException>(() => terminal.ExecuteAsync(context, NullLogger<YieldCurveRateEventActor>.Instance).AsTask());
        Assert.Equal(o, error.Outcome);
        Assert.DoesNotContain(context.ReceivedCalls(), c => c.GetMethodInfo().Name == "SendAsync");
    }

    [Theory]
    [InlineData("Command ID 11111111-1111-1111-1111-111111111111 is already associated with a different command payload.")]
    [InlineData("A different terminal outcome is already committed for this import attempt.")]
    public async Task Given_an_immutable_outcome_conflict_when_forwarding_then_the_terminal_is_not_retried(
        string errorMessage)
    {
        var outcome = DownloadLogContractTests.Outcome(MarketDataDownloadDataset.TreasuryCurve);
        var context = Substitute.For<IEventActorContext>();
        context.RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId>(
                Arg.Any<InsertMarketDataDownloadLogCommand>())
            .Returns(new ServiceFailed<GuidResult>(InsertMarketDataDownloadLogCommand.ErrorId, errorMessage));
        var terminal = new YieldCurveRatesImportedCompleteEvent
        {
            ImportDate = outcome.ValueDate.ToDateTime(TimeOnly.MinValue),
            Id = outcome.SourceTerminalEventId,
            CommandId = outcome.ImportCommandId,
            DownloadOutcome = outcome
        };

        var result = await terminal.ExecuteAsync(
            context,
            NullLogger<YieldCurveRateEventActor>.Instance);

        Assert.True(result);
        await context.Received(1).RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId>(
            Arg.Any<InsertMarketDataDownloadLogCommand>());
    }

    [Theory] [InlineData("empty")] [InlineData("provider-failed")] [InlineData("write-failed")] [InlineData("notification-failed")]
    public async Task Given_an_import_when_processing_ends_then_counts_and_delivery_failures_have_distinct_meanings(string scenario)
    {
        var api = Substitute.For<IReferenceDataApi>(); var provider = Substitute.For<ITreasuryCurve>(); api.TreasuryCurve.Returns(provider);
        var factory = Substitute.For<IDbContextFactory>(); var db = Substitute.For<IMarketDataDbContext>(); factory.MarketDataDb.Returns(db);
        var context = Substitute.For<IEventActorContext>(); var date = new DateOnly(2026, 9, 5);
        provider.GetRangeAsync(date, date, Arg.Any<CancellationToken>()).Returns(scenario == "provider-failed"
            ? Task.FromException<IReadOnlyList<TreasuryCurveSnapshot>>(new InvalidOperationException("acquisition failed"))
            : Task.FromResult<IReadOnlyList<TreasuryCurveSnapshot>>([]));
        db.InsertYieldCurveRatesAsync(Arg.Any<YieldCurveRateReadModel[]>(), Arg.Any<ImportDuplicatePolicy>(), Arg.Any<Guid>())
            .Returns(scenario == "write-failed" ? Task.FromException(new InvalidOperationException("partial write")) : Task.CompletedTask);
        YieldCurveRatesImportedCompleteEvent? completed = null; YieldCurveRatesImportedFailEvent? failed = null;
        context.SendAsync<YieldCurveRatesImportedCompleteEvent, YieldCurveRateEntityId>(Arg.Do<YieldCurveRatesImportedCompleteEvent>(e => completed = e))
            .Returns(_ => scenario == "notification-failed" ? ValueTask.FromException(new InvalidOperationException("notification unavailable")) : ValueTask.CompletedTask);
        context.SendAsync<YieldCurveRatesImportedFailEvent, YieldCurveRateEntityId>(Arg.Do<YieldCurveRatesImportedFailEvent>(e => failed = e)).Returns(ValueTask.CompletedTask);
        var request = new YieldCurveRatesImportedEvent { CommandId = Guid.NewGuid(), EntityId = new(2026), ImportDate = date.ToDateTime(TimeOnly.MinValue), RequestedOn = DateTime.UtcNow,
            Subject = new(ActorType.Event, YieldCurveRatesImportedEvent.Actor, YieldCurveRatesImportedEvent.Verb, "2026") };
        var exception = await Record.ExceptionAsync(() => request.ExecuteAsync(context, api, factory, NullLogger<YieldCurveRateEventActor>.Instance).AsTask());
        if (scenario == "notification-failed") Assert.NotNull(exception); else Assert.Null(exception);
        var outcome = (completed?.DownloadOutcome ?? failed?.DownloadOutcome)!; outcome.Validate();
        Assert.Equal(request.CommandId, outcome.ImportCommandId); Assert.True(outcome.ElapsedMilliseconds >= 0);
        if (scenario is "empty" or "notification-failed")
        {
            Assert.Null(failed); Assert.Equal(MarketDataDownloadStatus.Completed, outcome.Status); Assert.Equal(0, outcome.PersistedRecordCount);
        }
        if (scenario == "write-failed") { Assert.Null(outcome.PersistedRecordCount); Assert.Equal(0, outcome.DownloadedRecordCount); }
        if (scenario == "provider-failed") { Assert.Null(outcome.DownloadedRecordCount); Assert.Equal(0, outcome.PersistedRecordCount); }
    }

    [Fact]
    public async Task Given_a_terminal_event_is_redelivered_then_the_exact_logging_command_is_reused()
    {
        var outcome = DownloadLogContractTests.Outcome(MarketDataDownloadDataset.TreasuryCurve);
        var context = Substitute.For<IEventActorContext>();
        var captured = new List<InsertMarketDataDownloadLogCommand>();
        context.RequestAsync<InsertMarketDataDownloadLogCommand, DownloadLogId>(
                Arg.Do<InsertMarketDataDownloadLogCommand>(command => captured.Add(command)))
            .Returns(new ServiceOk<GuidResult>(new GuidResult(
                MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId))));
        var terminal = new YieldCurveRatesImportedCompleteEvent
        {
            ImportDate = outcome.ValueDate.ToDateTime(TimeOnly.MinValue),
            Id = outcome.SourceTerminalEventId,
            CommandId = outcome.ImportCommandId,
            DownloadOutcome = outcome
        };

        Assert.True(await terminal.ExecuteAsync(context, NullLogger<YieldCurveRateEventActor>.Instance));
        Assert.True(await terminal.ExecuteAsync(context, NullLogger<YieldCurveRateEventActor>.Instance));

        Assert.Equal(2, captured.Count);
        Assert.Equal(captured[0], captured[1]);
        Assert.Equal(captured[0].PayloadSha256, captured[1].PayloadSha256);
    }
}
