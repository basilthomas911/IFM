using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.QueryParameters;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public class FundQueryApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IFundQueryApi
{
    /// <summary>
    /// Gets the closing fund balance for a specific fund and value date.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The closing fund balance view model.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetClosingFundBalanceAsync(int fundId, DateOnly valueDate)
    {
        var entityId = new GetClosingFundBalanceParameter(fundId, valueDate);
        GetClosingFundBalanceQuery query = new(fundId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetClosingFundBalanceQuery.Actor, GetClosingFundBalanceQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetClosingFundBalanceQuery, FundBalanceReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the current fund balance for a specific fund.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <returns>The fund balance view model.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetFundBalanceAsync(int fundId)
    {
        var entityId = new GetFundBalanceParameter(fundId);
        GetFundBalanceQuery query = new(fundId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFundBalanceQuery.Actor, GetFundBalanceQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFundBalanceQuery, FundBalanceReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the fund drawdown balances for a specific fund and date range.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>The fund drawdown balances view model.</returns>
    public async Task<ServiceResult<FundDrawdownBalancesReadModel>> GetFundDrawdownBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var entityId = new GetFundDrawdownBalancesParameter(fundId, startDate, endDate);
        GetFundDrawdownBalancesQuery query = new(fundId, startDate, endDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFundDrawdownBalancesQuery.Actor, GetFundDrawdownBalancesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFundDrawdownBalancesQuery, FundDrawdownBalancesReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the fund ID from an order ID.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <returns>The scalar view model containing the fund ID.</returns>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetFundIdFromOrderIdAsync(int orderId)
    {
        var entityId = new GetFundIdFromOrderIdParameter(orderId);
        GetFundIdFromOrderIdQuery query = new(orderId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFundIdFromOrderIdQuery.Actor, GetFundIdFromOrderIdQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFundIdFromOrderIdQuery, ScalarReadModel<int>>(query.Subject, query);
    }

    /// <summary>
    /// Gets all fund orders.
    /// </summary>
    /// <returns>An array of fund order view models.</returns>
    public async Task<ServiceResult<FundOrderReadModel[]>> GetFundOrdersAsync()
    {
        var entityId = new GetFundOrdersParameter();
        GetFundOrdersQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetFundOrdersQuery.Actor, GetFundOrdersQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFundOrdersQuery, FundOrderReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Gets all fund order trades.
    /// </summary>
    /// <returns>An array of fund order trade view models.</returns>
    public async Task<ServiceResult<FundOrderTradeReadModel[]>> GetFundOrderTradesAsync()
    {
        var entityId = new GetFundOrderTradesParameter();
        GetFundOrderTradesQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetFundOrderTradesQuery.Actor, GetFundOrderTradesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFundOrderTradesQuery, FundOrderTradeReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Gets the fund PnL report for a specific fund and date range.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>The fund PnL report view model.</returns>
    public async Task<ServiceResult<FundPnlReportReadModel>> GetFundPnlReportAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var entityId = new GetFundPnlReportParameter(fundId, startDate, endDate);
        GetFundPnlReportQuery query = new(fundId, startDate, endDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFundPnlReportQuery.Actor, GetFundPnlReportQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFundPnlReportQuery, FundPnlReportReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets all funds.
    /// </summary>
    /// <returns>An array of fund view models.</returns>
    public async Task<ServiceResult<FundReadModel[]>> GetFundsAsync()
    {
        var entityId = new GetFundsParameter();
        GetFundsQuery query = new()
        {
            Subject = new ActorSubject(ActorType.Query, GetFundsQuery.Actor, GetFundsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFundsQuery, FundReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Gets all fund transactions for a specific fund and date range.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>An array of fund transaction view models.</returns>
    public async Task<ServiceResult<FundTransactionReadModel[]>> GetFundTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var entityId = new GetFundTransactionsParameter(fundId, startDate, endDate);
        GetFundTransactionsQuery query = new(fundId, startDate, endDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFundTransactionsQuery.Actor, GetFundTransactionsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFundTransactionsQuery, FundTransactionReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Gets the fund win/loss ratio for a specific fund and date range.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>The fund win/loss ratio view model.</returns>
    public async Task<ServiceResult<FundWinLossRatioReadModel>> GetFundWinLossRatioAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var entityId = new GetFundWinLossRatioParameter(fundId, startDate, endDate);
        GetFundWinLossRatioQuery query = new(fundId, startDate, endDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFundWinLossRatioQuery.Actor, GetFundWinLossRatioQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetFundWinLossRatioQuery, FundWinLossRatioReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Gets the opening fund balance for a specific fund and value date.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>The opening fund balance view model.</returns>
    public async Task<ServiceResult<FundBalanceReadModel>> GetOpeningFundBalanceAsync(int fundId, DateOnly valueDate)
    {
        var entityId = new GetOpeningFundBalanceParameter(fundId, valueDate);
        GetOpeningFundBalanceQuery query = new(fundId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetOpeningFundBalanceQuery.Actor, GetOpeningFundBalanceQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetOpeningFundBalanceQuery, FundBalanceReadModel>(query.Subject, query);
    }
}
