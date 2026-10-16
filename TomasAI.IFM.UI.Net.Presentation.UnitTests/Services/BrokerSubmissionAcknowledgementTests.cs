using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.Trade;
namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

public sealed class BrokerSubmissionAcknowledgementTests
{
    static TradeOrderDefinition Order(Guid component) => new() { Id=new(1,2,3), Components=[new() { ComponentId=component }] };
    static BrokerOrderDefinition Broker(TradeOrderDefinition order, Guid component, BrokerOrderStatus status) => new() {
        Id=new(new(order.Id,Guid.NewGuid()),component), Status=status,
        LastObservation=new() { Category="BROKER.123", Detail="The order was rejected by the broker" }
    };
    [Theory]
    [InlineData(BrokerOrderStatus.Working)]
    [InlineData(BrokerOrderStatus.PartiallyFilled)]
    [InlineData(BrokerOrderStatus.Filled)]
    public async Task Broker_observation_accepts_the_exact_order_component(BrokerOrderStatus status)
    {
        var component=Guid.NewGuid(); var order=Order(component); var queries=Substitute.For<IBrokerOrderQueryApi>();
        queries.ListAsync(order.Id,Arg.Any<CancellationToken>()).Returns(new ServiceOk<BrokerOrderDefinition[]>([Broker(order,component,status)]));
        await BrokerSubmissionAcknowledgement.WaitAsync(queries,[order]);
    }
    [Fact]
    public async Task Broker_rejection_preserves_the_broker_error()
    {
        var component=Guid.NewGuid(); var order=Order(component); var queries=Substitute.For<IBrokerOrderQueryApi>();
        queries.ListAsync(order.Id,Arg.Any<CancellationToken>()).Returns(new ServiceOk<BrokerOrderDefinition[]>([Broker(order,component,BrokerOrderStatus.Rejected)]));
        var act=()=>BrokerSubmissionAcknowledgement.WaitAsync(queries,[order]);
        await act.Should().ThrowAsync<BrokerOrderRejectedException>().WithMessage("BROKER.123: The order was rejected by the broker");
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispatch_or_acknowledgement_of_another_component_is_not_acceptance(bool wrongComponent)
    {
        var component=Guid.NewGuid(); var order=Order(component); var queries=Substitute.For<IBrokerOrderQueryApi>();
        queries.ListAsync(order.Id,Arg.Any<CancellationToken>()).Returns(new ServiceOk<BrokerOrderDefinition[]>([
            Broker(order,wrongComponent?Guid.NewGuid():component,wrongComponent?BrokerOrderStatus.Working:BrokerOrderStatus.Dispatched)]));
        var act=()=>BrokerSubmissionAcknowledgement.WaitAsync(queries,[order],timeout:TimeSpan.FromMilliseconds(30));
        await act.Should().ThrowAsync<TimeoutException>().WithMessage("*unconfirmed*");
    }
}
