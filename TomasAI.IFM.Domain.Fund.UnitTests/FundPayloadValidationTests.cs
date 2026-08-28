using FluentAssertions;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

public sealed class FundPayloadValidationTests
{
    [Fact]
    public void Fund_rules_report_all_invalid_payload_properties()
    {
        var fund = new FundReadModel(
            0,
            string.Empty,
            null!,
            -1m,
            false,
            DateTime.MinValue,
            string.Empty);

        var messages = new List<ValidationError>()
            .ValidateFund(fund)
            .Select(error => error.ErrorMessage)
            .ToArray();

        messages.Should().Contain(message => message.Contains("FundId"));
        messages.Should().Contain(message => message.Contains("Name"));
        messages.Should().Contain(message => message.Contains("Description"));
        messages.Should().Contain(message => message.Contains("Balance"));
        messages.Should().Contain(message => message.Contains("CreatedOn"));
        messages.Should().Contain(message => message.Contains("CreatedBy"));
    }

    [Fact]
    public void Fund_order_rules_report_identifiers_enums_dates_and_required_text()
    {
        var order = new FundOrderReadModel(
            0,
            0,
            DateTime.MinValue,
            (Shared.OrderStatus)999,
            string.Empty,
            DateOnly.MinValue,
            DateOnly.MinValue,
            null!,
            DateTime.MinValue,
            string.Empty,
            DateTime.MinValue,
            string.Empty);

        var messages = new List<ValidationError>()
            .ValidateFundOrder(order)
            .Select(error => error.ErrorMessage)
            .ToArray();

        messages.Should().Contain(message => message.Contains("FundId"));
        messages.Should().Contain(message => message.Contains("OrderId"));
        messages.Should().Contain(message => message.Contains("OrderStatus"));
        messages.Should().Contain(message => message.Contains("BaseContractId"));
        messages.Should().Contain(message => message.Contains("TradeDate"));
        messages.Should().Contain(message => message.Contains("MaturityDate"));
        messages.Should().Contain(message => message.Contains("Reference"));
        messages.Should().Contain(message => message.Contains("CreatedOn"));
        messages.Should().Contain(message => message.Contains("CreatedBy"));
        messages.Should().Contain(message => message.Contains("UpdatedOn"));
        messages.Should().Contain(message => message.Contains("UpdatedBy"));
    }

    [Fact]
    public void Fund_order_trade_rules_report_identifiers_enums_dates_and_required_text()
    {
        var trade = new FundOrderTradeReadModel(
            0,
            0,
            0,
            TradeType.Unknown,
            DateOnly.MinValue,
            DateOnly.MinValue,
            (TradeState)999,
            (TradeAction)999,
            string.Empty,
            false,
            string.Empty,
            DateTime.MinValue,
            string.Empty,
            DateTime.MinValue,
            string.Empty);

        var messages = new List<ValidationError>()
            .ValidateFundOrderTrade(trade)
            .Select(error => error.ErrorMessage)
            .ToArray();

        messages.Should().Contain(message => message.Contains("FundId"));
        messages.Should().Contain(message => message.Contains("OrderId"));
        messages.Should().Contain(message => message.Contains("TradeId"));
        messages.Should().Contain(message => message.Contains("TradeType"));
        messages.Should().Contain(message => message.Contains("TradeDate"));
        messages.Should().Contain(message => message.Contains("MaturityDate"));
        messages.Should().Contain(message => message.Contains("TradeState"));
        messages.Should().Contain(message => message.Contains("TradeAction"));
        messages.Should().Contain(message => message.Contains("Reference"));
        messages.Should().Contain(message => message.Contains("BaseContractSymbol"));
        messages.Should().Contain(message => message.Contains("CreatedOn"));
        messages.Should().Contain(message => message.Contains("CreatedBy"));
        messages.Should().Contain(message => message.Contains("UpdatedOn"));
        messages.Should().Contain(message => message.Contains("UpdatedBy"));
    }
}
