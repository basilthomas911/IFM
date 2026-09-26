using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

/// <summary>
/// Defines option-pricer database queries.
/// </summary>
public interface IOptionPricerDbReadContext
{
    /// <summary>Gets all configured option-pricer devices.</summary>
    /// <returns>The configured option-pricer devices.</returns>
    Task<ICollection<OptionPricerDeviceReadModel>> GetOptionPricerDevicesAsync();

    /// <summary>Gets all configured option-pricer devices.</summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The configured option-pricer devices.</returns>
    Task<ICollection<OptionPricerDeviceReadModel>> GetOptionPricerDevicesAsync(CancellationToken cancellationToken);

    /// <summary>Gets a spread distribution matching the supplied trade dimensions.</summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade type.</param>
    /// <param name="tradeStatus">The trade status.</param>
    /// <param name="valueDate">The distribution value date.</param>
    /// <param name="daysToExpiry">The number of days to expiry.</param>
    /// <returns>The matching spread distribution, or <see langword="null"/> when none exists.</returns>
    Task<SpreadDistributionReadModel?> GetSpreadDistributionAsync(
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry);

    /// <summary>Gets a spread distribution matching the supplied trade dimensions.</summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade type.</param>
    /// <param name="tradeStatus">The trade status.</param>
    /// <param name="valueDate">The distribution value date.</param>
    /// <param name="daysToExpiry">The number of days to expiry.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The matching spread distribution, or <see langword="null"/> when none exists.</returns>
    Task<SpreadDistributionReadModel?> GetSpreadDistributionAsync(
        int tradeId,
        TradeType tradeType,
        TradeStatus tradeStatus,
        DateOnly valueDate,
        int daysToExpiry,
        CancellationToken cancellationToken);

    /// <summary>Gets the number of in-progress spread-distribution jobs for an order and trade.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>The number of matching in-progress jobs.</returns>
    Task<int> GetSpreadDistributionJobInProgressCountAsync(int orderId, int tradeId);

    /// <summary>Gets the number of in-progress spread-distribution jobs for an order and trade.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous query.</param>
    /// <returns>The number of matching in-progress jobs.</returns>
    Task<int> GetSpreadDistributionJobInProgressCountAsync(
        int orderId,
        int tradeId,
        CancellationToken cancellationToken);
}
