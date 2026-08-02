using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Event.Api;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.UnitTests.Event.Api;

public class ActorFundEventApiTests
{
    [Fact]
    public async Task FactoryBindsTheContextAndSendsTypedCompleteEvent()
    {
        var context = Substitute.For<IEventActorContext>();
        var source = SampleData.FundMaxProfitGeneratedEvent;
        var api = new ActorFundEventApiFactory().Create(context);

        await api.SendFundMaxProfitGeneratedCompleteAsync(source);

        api.Should().BeAssignableTo<IActorFundEventApi>();
        await context.Received(1).SendAsync<FundMaxProfitGeneratedCompleteEvent, FundId>(
            Arg.Is<FundMaxProfitGeneratedCompleteEvent>(sent =>
                sent.CommandId == source.CommandId &&
                sent.Subject.Is(
                    ActorType.Event,
                    FundMaxProfitGeneratedCompleteEvent.Actor,
                    FundMaxProfitGeneratedCompleteEvent.Verb)));
    }

    [Fact]
    public async Task FailureMethodConvertsExceptionToTypedFailEvent()
    {
        var context = Substitute.For<IEventActorContext>();
        var source = SampleData.FundMaxProfitGeneratedEvent;
        var api = new ActorFundEventApi(context);

        await api.SendFundMaxProfitGeneratedFailAsync(
            source,
            new InvalidOperationException("fund calculation failed"));

        await context.Received(1).SendAsync<FundMaxProfitGeneratedFailEvent, FundId>(
            Arg.Is<FundMaxProfitGeneratedFailEvent>(sent =>
                sent.CommandId == source.CommandId &&
                sent.ErrorMessage == "fund calculation failed"));
    }
}
