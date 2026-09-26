using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb;

/// <summary>
/// Defines option-pricer database commands.
/// </summary>
public interface IOptionPricerDbWriteContext
{
    /// <summary>Deletes an option-pricer device.</summary>
    Task DeleteOptionPricerDeviceAsync(OptionPricerDeviceEntityId deviceId);

    /// <summary>Deletes spread distributions for a trade and value date.</summary>
    Task DeleteSpreadDistributionAsync(int tradeId, DateOnly valueDate);

    /// <summary>Deletes all spread-distribution jobs for an order and trade.</summary>
    Task DeleteSpreadDistributionJobsAsync(int orderId, int tradeId);

    /// <summary>Deletes every spread-distribution job currently marked as in progress.</summary>
    Task DeleteSpreadDistributionJobsInProgressAsync();

    /// <summary>Inserts an option-pricer device configuration.</summary>
    Task InsertOptionPricerDeviceAsync(OptionPricerDeviceReadModel device);

    /// <summary>Inserts the put-side and call-side spread distributions as one queued operation.</summary>
    Task InsertSpreadDistributionsAsync(
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution);

    /// <summary>Inserts a spread-distribution job.</summary>
    Task InsertSpreadDistributionJobAsync(SpreadDistributionJobReadModel job);

    /// <summary>Updates the persisted status of a spread-distribution job.</summary>
    Task UpdateSpreadDistributionJobStatusAsync(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        SpreadDistributionJobStatus jobStatus,
        DateTime jobCompleted,
        DateTime? jobFailed = null);
}
