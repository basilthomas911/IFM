using NSubstitute;
using TomasAI.IFM.Domain.Fund.Event.Actor;
using TomasAI.IFM.Domain.Fund.Event.Extensions;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.UnitTests.Event.Extensions;

/// <summary>
/// Verifies Fund event conversion and publication extensions.
/// </summary>
public sealed class FundEventExtensionsTests
{
    [Fact]
    public async Task Complete_extension_sends_typed_complete_event()
    {
        var context = Substitute.For<IFundEventContext>();
        var source = SampleData.FundMaxProfitGeneratedEvent;

        await context.SendFundMaxProfitGeneratedCompleteAsync(source);

        await context.Received(1).SendAsync<FundMaxProfitGeneratedCompleteEvent, FundId>(
            Arg.Is<FundMaxProfitGeneratedCompleteEvent>(sent =>
                sent.CommandId == source.CommandId
                && sent.Subject.Is(
                    ActorType.Event,
                    FundMaxProfitGeneratedCompleteEvent.Actor,
                    FundMaxProfitGeneratedCompleteEvent.Verb)));
    }

    [Fact]
    public async Task Failure_extension_sends_typed_failure_event()
    {
        var context = Substitute.For<IFundEventContext>();
        var source = SampleData.FundMaxProfitGeneratedEvent;

        await context.SendFundMaxProfitGeneratedFailAsync(
            source,
            new InvalidOperationException("fund calculation failed"));

        await context.Received(1).SendAsync<FundMaxProfitGeneratedFailEvent, FundId>(
            Arg.Is<FundMaxProfitGeneratedFailEvent>(sent =>
                sent.CommandId == source.CommandId
                && sent.ErrorMessage == "fund calculation failed"));
    }
}
