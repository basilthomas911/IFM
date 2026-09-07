using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

[Trait("Gate","MC-R04")]
public sealed class MarketConditionAssessmentProviderTests
{
    [Fact]
    public async Task Production_capture_uses_underlying_quote_health_and_confirmed_calendar_without_optional_feeds()
    {
        var f = new Fixture(); var s = await f.Capture();
        var r = new MarketConditionAssessmentCalculator().Calculate(f.Command, s, f.Command.CommandId);
        r.Assessment.Availability.Should().Be(AssessmentAvailability.Available);
        r.Assessment.StressState.Should().Be(AssessmentStress.Unknown);
        r.Assessment.EventRiskState.Should().Be(AssessmentEventContext.Elevated);
        s.ReferenceInstrumentId.Should().Be("ES-test"); s.PayloadSha256.Should().Be(s.ComputeHash());
        f.Events.Received(1).ReadOnceAsync(Arg.Any<MarketConditionEventRiskConfiguration>(), f.At, Arg.Any<CancellationToken>());
        var calls = f.Market.ReceivedCalls().Select(x=>x.GetMethodInfo().Name).Distinct();
        calls.Should().BeSubsetOf(["TryGetOnTheRunFuturesContract","TryGetLastTickPrice","GetFuturesMarketHealth"]);
    }

    [Fact]
    public async Task Changing_feed_generation_exhausts_bounded_capture_instead_of_sealing_mixed_sources()
    {
        var f = new Fixture(); var sequence = 0;
        f.Market.GetFuturesMarketHealth("ES-test").Returns(_=>f.Health with { Generation=(++sequence).ToString() });
        await FluentActions.Awaiting(async()=>await f.Capture()).Should().ThrowAsync<InvalidOperationException>();
        sequence.Should().Be(f.Command.ParameterSet.SnapshotCaptureAttempts*2);
    }

    [Fact]
    public async Task Calendar_query_exception_is_a_capture_failure_not_an_unavailable_market_report()
    {
        var f = new Fixture();
        f.Events.ReadOnceAsync(Arg.Any<MarketConditionEventRiskConfiguration>(),f.At,Arg.Any<CancellationToken>())
            .Returns(_=>ValueTask.FromException<MarketConditionEventRiskState>(new InvalidOperationException("query failed")));
        await FluentActions.Awaiting(async()=>await f.Capture()).Should().ThrowAsync<InvalidOperationException>();
    }

    sealed class Fixture
    {
        public readonly TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteMarketConditionAssessmentCommand Command = AssessmentFixture.Command();
        public DateTime At=>Command.RequestedAtUtc;
        public readonly IMarketDataApi Market=Substitute.For<IMarketDataApi>();
        public readonly IMarketConditionEventRiskAdapter Events=Substitute.For<IMarketConditionEventRiskAdapter>();
        readonly IDbContextFactory storage=Substitute.For<IDbContextFactory>();
        readonly IMarketSessionCalendar calendar=Substitute.For<IMarketSessionCalendar>();
        public FuturesMarketHealthSnapshot Health=>new(true,true,"test-generation",DateOnly.FromDateTime(At),new(At),1);
        public Fixture()
        {
            Market.TryGetOnTheRunFuturesContract("ES",out Arg.Any<FuturesContractV3ReadModel>()).Returns(x=>{x[1]=new FuturesContractV3ReadModel { ContractId="ES-test",Symbol="ES" };return true;});
            Market.GetFuturesMarketHealth("ES-test").Returns(_=>Health);
            Market.TryGetLastTickPrice("ES-test",out Arg.Any<FuturesMarketPriceSnapshot>()).Returns(x=>
            {x[1]=new FuturesMarketPriceSnapshot("ES-test",1,1,default,DateOnly.FromDateTime(At),new FuturesMarketQuoteSnapshot(5000,10,5000.25m,10,1,1,1,new(At),new(At)),null);return true;});
            calendar.GetValueDate(Arg.Any<DateTimeOffset>()).Returns(DateOnly.FromDateTime(At));
            Events.ReadOnceAsync(Arg.Any<MarketConditionEventRiskConfiguration>(),At,Arg.Any<CancellationToken>()).Returns(new MarketConditionEventRiskState
            {
                Status=MarketEventRiskStatus.Blocked,
                Observation=new(){SourceTimestampUtc=At,ReceivedAtUtc=At,SequenceId=1,Availability=MarketSourceAvailability.Available,Validity=MarketSourceValidity.Valid},
                DownloadEvidence=new(){CheckedAtUtc=At,CoverageConfirmed=true,ValidUntilUtc=At.AddHours(1)}
            });
        }
        public ValueTask<MarketConditionAssessmentSnapshot> Capture()=>new MarketConditionAssessmentSnapshotProvider(Market,storage,calendar,Events).CaptureAsync(Command.ParameterSet,At,default);
    }
}
