using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Command.Api;

public class ActorTradeCommandApiTests
{
    [Fact]
    public async Task ChangeSpreadStatisticsUsesTheBoundEventContextAndReturnsItsResult()
    {
        var context = Substitute.For<IEventActorContext>();
        var expected = new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()));
        var valueDate = new DateOnly(2026, 8, 2);
        context.RequestAsync<ChangeOptionTradeSpreadDistributionStatisticsCommand, OptionTradeEntityId>(
                Arg.Any<ChangeOptionTradeSpreadDistributionStatisticsCommand>())
            .Returns(expected);
        var api = context;

        var result = await api.ChangeSpreadDistributionStatisticsAsync(101, 7, 0.35, 0.21, valueDate);

        result.Should().BeSameAs(expected);
        await context.Received(1)
            .RequestAsync<ChangeOptionTradeSpreadDistributionStatisticsCommand, OptionTradeEntityId>(
                Arg.Is<ChangeOptionTradeSpreadDistributionStatisticsCommand>(command =>
                    command.OrderId == 101 &&
                    command.TradeId == 7 &&
                    command.ForwardLossRatio == 0.35 &&
                    command.LossProbability == 0.21 &&
                    command.ValueDate == valueDate &&
                    command.CommandId != Guid.Empty &&
                    command.ErrorCode == ChangeOptionTradeSpreadDistributionStatisticsCommand.ErrorId &&
                    command.Subject.Is(
                        ActorType.Command,
                        ChangeOptionTradeSpreadDistributionStatisticsCommand.Actor,
                        ChangeOptionTradeSpreadDistributionStatisticsCommand.Verb)));
    }

    [Fact]
    public async Task FailedCommandResultIsRaisedToTheCallingEventHandler()
    {
        var context = Substitute.For<IEventActorContext>();
        context.RequestAsync<ChangeOptionTradeSpreadDistributionStatisticsCommand, OptionTradeEntityId>(
                Arg.Any<ChangeOptionTradeSpreadDistributionStatisticsCommand>())
            .Returns(new ServiceFailed<GuidResult>(
                ChangeOptionTradeSpreadDistributionStatisticsCommand.ErrorId,
                "trade update failed"));
        var api = context;

        Func<Task> act = async () => await api.ChangeSpreadDistributionStatisticsAsync(
            101, 7, 0.35, 0.21, new DateOnly(2026, 8, 2));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("trade update failed");
    }
}
