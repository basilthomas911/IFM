using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using System.Collections.Concurrent;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>One UI query boundary for stored New Trade initialization. Selection and quantities remain local.</summary>
public static class BrokerTradeInitializationQuery
{
    public static async Task<BrokerTradeInitializationResult> ExecuteAsync(IAppRoot root,
        int portfolioId, int fundId, int orderId, int tradeId, string underlyingContractId, DateOnly valueDate, IReadOnlyList<BrokerTradeLegData> selectedLegs, BrokerCapabilities capabilities, CancellationToken token = default)
    {
        var errors = new ConcurrentQueue<string>();
        var scope = new FinancialReadScope { PortfolioId = portfolioId, FundId = fundId,
            Access = new(Environment.UserName, ["LedgerRead"], [portfolioId]) };
        var balances = Read("Fund balances", () => root.Services.PortfolioFinancial.GetAccountBalancesAsync(scope, new(), token));
        var usage = Read("Fund capacity usage", () => root.Services.PortfolioFinancial.GetCapacityUsageAsync(scope, new(), token));
        var risk = Read("Fund risk envelope", () => root.Services.PortfolioQueries.GetFundRiskEnvelopeAsync(portfolioId, fundId, DateTime.UtcNow, token));
        var account = Read("Broker account", () => root.Services.BrokerAccounts.GetAsync(new(capabilities.AccountAlias), token).AsTask());
        var order = Read("Order", () => root.Services.PortfolioQueries.GetOrderAsync(orderId, token));

        var reference = Read("Underlying definition", () => root.Services.MarketDataQueries.ResolveUnderlyingAsync(underlyingContractId, token));
        var price = Read("Stored underlying price", () => root.Services.MarketDataQueries.QueryFuturesEodDataAsync(underlyingContractId, valueDate, token));
        var tradeTask = selectedLegs.Any(x => x.IsFuture)
            ? Task.FromResult<OptionTradeReadModel?>(null)
            : Read("Stored trade", () => root.Services.TradeQueries.QueryOptionTradeAsync(orderId, tradeId, token));
        await Task.WhenAll(balances, usage, risk, order, reference, price, tradeTask, account).ConfigureAwait(false);
        var balancesData = await balances.ConfigureAwait(false);
        var usageData = await usage.ConfigureAwait(false);
        var riskData = await risk.ConfigureAwait(false);
        var orderData = await order.ConfigureAwait(false);
        var underlyingData = await reference.ConfigureAwait(false);
        var underlyingPriceData = await price.ConfigureAwait(false);
        var tradeData = await tradeTask.ConfigureAwait(false);
        var accountData = await account.ConfigureAwait(false);
        FinancialRead<FinancialReservationView>? reservation = null;
        if (orderData?.RiskAuthorization is { } authorization)
            reservation = await Read("Order reservation", () => root.Services.PortfolioFinancial.GetCapacityReservationAsync(
                scope, new(authorization.ReservationId), token)).ConfigureAwait(false);
        return new(portfolioId, fundId, orderId, tradeId, balancesData, usageData, riskData, orderData, tradeData, reservation, underlyingData, underlyingPriceData, selectedLegs.ToArray(), capabilities, accountData, errors.ToArray());

        async Task<T?> Read<T>(string source, Func<Task<ServiceResult<T>>> load) where T : class
        {
            try
            {
                var result = await load().ConfigureAwait(false);
                if (result.Success) return result.Value;
                if (source != "Stored trade" || result.ErrorCode != 404) errors.Enqueue($"{source}: {result.ErrorMessage}");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception exception) { errors.Enqueue($"{source}: {exception.Message}"); }
            return null;
        }

    }
}

public sealed record BrokerTradeInitializationResult(
    int PortfolioId, int FundId, int OrderId, int TradeId,
    FinancialRead<FinancialBalanceSnapshot>? Balances,
    FinancialRead<FinancialCapacityUsage>? CapacityUsage,
    FundRiskEnvelopeReadModel? RiskEnvelope,
    FundOrderProjectionReadModel? Order,
    OptionTradeReadModel? Trade,
    FinancialRead<FinancialReservationView>? Reservation,
    FuturesContractV3ReadModel? Underlying,
    FuturesEodDataV2ReadModel? UnderlyingPrice,
    BrokerTradeLegData[] SelectedLegs,
    BrokerCapabilities Capabilities,
    BrokerAccountDefinition? BrokerAccount,
    string[] Errors);

/// <summary>Immutable selection input; quantity edits remain an explicit draft owned by the view.</summary>
public sealed record BrokerTradeLegData(string ContractId, string Role, decimal? Strike,
    decimal? Bid, decimal? Ask, double? Delta, bool IsCall,
    double? Vega = null, decimal? Multiplier = null, DateTimeOffset? QuoteAtUtc = null,
    bool Frozen = false, bool IsFuture = false)
{
    public int Sign => Role.StartsWith("+", StringComparison.Ordinal) ? 1 : Role.StartsWith("-", StringComparison.Ordinal) ? -1 : 0;
}
