using FluentAssertions;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using BrokerEnvironment = TomasAI.IFM.Application.TradeBroker.Contracts.BrokerEnvironment;
namespace TomasAI.IFM.Domain.Trade.UnitTests;

public sealed class DevelopmentBrokerQualificationTests
{
    [Theory]
    [InlineData(true, BrokerEnvironment.Emulator, true)]
    [InlineData(false, BrokerEnvironment.Emulator, false)]
    [InlineData(true, BrokerEnvironment.Paper, false)]
    [InlineData(true, BrokerEnvironment.Live, false)]
    public void Account_query_policy_cannot_exempt_production_or_external_brokers(bool development, BrokerEnvironment environment, bool exempt)
    {
        var stored = new BrokerAccountDefinition { Environment = environment, QualificationStatus = BrokerAccountQualificationStatus.EvidencePending,
            Gate = BrokerAccountOperationalGate.Closed, DevelopmentQualificationsExempt = true };
        var account = BrokerAccountTradingQualifications.TradingView(stored, development);
        account.DevelopmentQualificationsExempt.Should().Be(exempt);
        account.QualificationStatus.Should().Be(BrokerAccountQualificationStatus.EvidencePending);
        account.ApprovalId.Should().BeEmpty();
        var command = new CreateBrokerOrderCommand { Order = new() { BrokerEnvironment = TomasAI.IFM.Domain.Trade.Shared.BrokerEnvironment.Emulator,
            PositionType = TradeOrderPositionType.Opening } };
        var decision = BrokerOrderComputation.CreateBrokerOrder(command, null, account);
        decision.RejectionReason.Should().Be(exempt ? "BrokerOrder.APPROVAL.INCOMPLETE" : "BrokerOrder.ACCOUNT.SNAPSHOT_INCOMPLETE");
    }
}
