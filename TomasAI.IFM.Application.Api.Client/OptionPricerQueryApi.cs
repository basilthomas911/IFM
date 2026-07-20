using System.Threading.Tasks;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.Queries;
using TomasAI.IFM.Shared.OptionPricer.QueryParameters;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// REST client for option pricer queries. Delegates to an <see cref="IQueryServiceApi"/> and uses
/// the URI paths defined in <see cref="OptionPricerQueryUriPath"/>.
/// </summary>
public class OptionPricerQueryApi(IQueryServiceApi querySvc) : IOptionPricerQueryApi
{
    readonly IQueryServiceApi _querySvc = IsArgumentNull.Set(querySvc);

    /// <summary>
    /// Retrieves configured option pricer devices.
    /// </summary>
    public async Task<ServiceResult<OptionPricerDevicesReadModel>> GetOptionPricerDevicesAsync()
    {
        var qryParam = new GetOptionPricerDevicesParameter();
        return await _querySvc.ExecuteQueryAsync<OptionPricerDevicesReadModel>(OptionPricerQueryUriPath.GetOptionPricerDevices, qryParam, GetOptionPricerDevicesQuery.ErrorId);
    }

    /// <summary>
    /// Retrieves spread distribution for a given trade.
    /// </summary>
    /// <param name="tradeId">Trade identifier.</param>
    /// <param name="tradeType">Trade type.</param>
    /// <param name="tradeStatus">Trade status.</param>
    /// <param name="valueDate">Value date for the distribution.</param>
    /// <param name="daysToExpiry">Days to expiry.</param>
    public async Task<ServiceResult<SpreadDistributionReadModel>> GetSpreadDistributionAsync(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
    {
        var qryParam = new GetSpreadDistributionParameter(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry);
        return await _querySvc.ExecuteQueryAsync<SpreadDistributionReadModel>(OptionPricerQueryUriPath.GetSpreadDistribution, qryParam, GetSpreadDistributionQuery.ErrorId);
    }

    /// <summary>
    /// Determines whether a spread distribution job is currently in progress for the specified order and trade.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier.</param>
    public async Task<ServiceResult<ScalarReadModel<bool>>> IsSpreadDistributionJobInProgressAsync(int orderId, int tradeId)
    {
        var qryParam = new GetSpreadDistributionJobInProgressParameter(orderId, tradeId);
        return await _querySvc.ExecuteQueryAsync<ScalarReadModel<bool>>(OptionPricerQueryUriPath.IsSpreadDistributionJobInProgress, qryParam, GetSpreadDistributionJobInProgressQuery.ErrorId);
    }
}
