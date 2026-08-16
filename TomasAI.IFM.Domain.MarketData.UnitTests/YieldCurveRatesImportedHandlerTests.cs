using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Actor;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class YieldCurveRatesImportedHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_AcquiresBulkStoresAndCompletesTheAttempt()
    {
        var date = new DateOnly(2026, 8, 14);
        var snapshot = Curve(date);
        var (api, treasury, dbFactory, db, context) = Dependencies();
        treasury.GetRangeAsync(date, date, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TreasuryCurveSnapshot>>([snapshot]));
        YieldCurveRatesImportedCompleteEvent? completed = null;
        context.SendAsync<YieldCurveRatesImportedCompleteEvent, YieldCurveRateEntityId>(
                Arg.Do<YieldCurveRatesImportedCompleteEvent>(value => completed = value))
            .Returns(ValueTask.CompletedTask);
        var request = Request(date, ImportDuplicatePolicy.Reject);

        var result = await request.ExecuteAsync(
            context, api, dbFactory, NullLogger<YieldCurveRateEventActor>.Instance);

        result.Should().BeTrue();
        await db.Received(1).InsertYieldCurveRatesAsync(
            Arg.Is<YieldCurveRateReadModel[]>(rows =>
                rows.Length == 1 && rows[0].ValueDate == date && rows[0].OneMonth == 1d),
            ImportDuplicatePolicy.Reject,
            request.CommandId);
        completed.Should().NotBeNull();
        completed!.CommandId.Should().Be(request.CommandId);
        completed.YieldCurveRates.Should().ContainSingle();
        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<YieldCurveRatesImportedFailEvent, YieldCurveRateEntityId>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyProviderResultIsSuccessfulAndStoresAnEmptyArray()
    {
        var date = new DateOnly(2026, 8, 15);
        var (api, treasury, dbFactory, db, context) = Dependencies();
        treasury.GetRangeAsync(date, date, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TreasuryCurveSnapshot>>([]));
        var request = Request(date, ImportDuplicatePolicy.Overwrite);

        await request.ExecuteAsync(
            context, api, dbFactory, NullLogger<YieldCurveRateEventActor>.Instance);

        await db.Received(1).InsertYieldCurveRatesAsync(
            Arg.Is<YieldCurveRateReadModel[]>(rows => rows.Length == 0),
            ImportDuplicatePolicy.Overwrite,
            request.CommandId);
        await context.Received(1).SendAsync<YieldCurveRatesImportedCompleteEvent, YieldCurveRateEntityId>(
            Arg.Is<YieldCurveRatesImportedCompleteEvent>(value => value.YieldCurveRates.Length == 0));
    }

    [Fact]
    public async Task ExecuteAsync_AcquisitionFailurePublishesFailAndDoesNotWriteStorage()
    {
        var date = new DateOnly(2026, 8, 16);
        var (api, treasury, dbFactory, db, context) = Dependencies();
        treasury.GetRangeAsync(date, date, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<TreasuryCurveSnapshot>>(
                new InvalidOperationException("provider unavailable")));
        var request = Request(date, ImportDuplicatePolicy.Overwrite);

        Func<Task> act = async () => await request.ExecuteAsync(
            context, api, dbFactory, NullLogger<YieldCurveRateEventActor>.Instance);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("provider unavailable");
        await db.DidNotReceiveWithAnyArgs().InsertYieldCurveRatesAsync(default!, default, default);
        await context.Received(1).SendAsync<YieldCurveRatesImportedFailEvent, YieldCurveRateEntityId>(
            Arg.Is<YieldCurveRatesImportedFailEvent>(value =>
                value.CommandId == request.CommandId
                && value.ImportDate == request.ImportDate
                && value.ErrorMessage == "provider unavailable"));
        await context.DidNotReceiveWithAnyArgs()
            .SendAsync<YieldCurveRatesImportedCompleteEvent, YieldCurveRateEntityId>(default!);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredTenorFailsBeforeStorage()
    {
        var date = new DateOnly(2026, 8, 16);
        var (api, treasury, dbFactory, db, context) = Dependencies();
        treasury.GetRangeAsync(date, date, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TreasuryCurveSnapshot>>([
                new TreasuryCurveSnapshot(
                    date,
                    Enum.GetValues<TreasuryTenor>()
                        .Where(tenor => tenor != TreasuryTenor.ThirtyYear)
                        .Select(tenor => new TreasuryRatePoint(tenor, 1m))
                        .ToArray(),
                    DateTimeOffset.UtcNow,
                    "test")
            ]));
        var request = Request(date, ImportDuplicatePolicy.Overwrite);

        Func<Task> act = async () => await request.ExecuteAsync(
            context, api, dbFactory, NullLogger<YieldCurveRateEventActor>.Instance);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing tenor*");
        await db.DidNotReceiveWithAnyArgs().InsertYieldCurveRatesAsync(default!, default, default);
        await context.Received(1).SendAsync<YieldCurveRatesImportedFailEvent, YieldCurveRateEntityId>(
            Arg.Any<YieldCurveRatesImportedFailEvent>());
    }

    static (IReferenceDataApi Api, ITreasuryCurve Treasury, IDbContextFactory Factory,
        IMarketDataDbContext Db, IEventActorContext Context) Dependencies()
    {
        var api = Substitute.For<IReferenceDataApi>();
        var treasury = Substitute.For<ITreasuryCurve>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IMarketDataDbContext>();
        var context = Substitute.For<IEventActorContext>();
        api.TreasuryCurve.Returns(treasury);
        dbFactory.MarketDataDb.Returns(db);
        db.InsertYieldCurveRatesAsync(
                Arg.Any<YieldCurveRateReadModel[]>(),
                Arg.Any<ImportDuplicatePolicy>(),
                Arg.Any<Guid>())
            .Returns(Task.CompletedTask);
        return (api, treasury, dbFactory, db, context);
    }

    static YieldCurveRatesImportedEvent Request(DateOnly date, ImportDuplicatePolicy policy)
    {
        var entityId = new YieldCurveRateEntityId(date.Year);
        return new YieldCurveRatesImportedEvent
        {
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            Subject = new ActorSubject(ActorType.Event, YieldCurveRatesImportedEvent.Actor,
                YieldCurveRatesImportedEvent.Verb, entityId.Format()),
            ImportDate = date.ToDateTime(TimeOnly.MinValue),
            RequestedOn = DateTime.UtcNow,
            RequestedBy = "test",
            DuplicatePolicy = policy
        };
    }

    static TreasuryCurveSnapshot Curve(DateOnly date) => new(
        date,
        Enum.GetValues<TreasuryTenor>()
            .Select((tenor, index) => new TreasuryRatePoint(tenor, index + 1m))
            .ToArray(),
        DateTimeOffset.UtcNow,
        "test");
}
