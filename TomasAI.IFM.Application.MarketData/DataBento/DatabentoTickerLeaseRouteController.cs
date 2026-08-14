using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>
/// Applies TickAggregation lease transitions to the DataBento transient route registries.
/// </summary>
internal sealed class DatabentoTickerLeaseRouteController(
    ITickLiveRouter liveRouter,
    DatabentoOptionRouteRegistry optionRoutes) : ITickerLeaseRouteController
{
    public void Activate(TickContractMapping mapping)
    {
        if (mapping.AssetTypeId == AssetTypeId.FuturesOption)
        {
            var reserved = optionRoutes.StartIndividual(mapping.ContractId);
            if (!reserved)
                return;

            try
            {
                liveRouter.Activate(mapping.ContractId);
            }
            catch
            {
                optionRoutes.StopIndividual(mapping.ContractId);
                throw;
            }
            return;
        }

        liveRouter.Activate(mapping.ContractId);
    }

    public void Deactivate(TickContractMapping mapping)
    {
        if (mapping.AssetTypeId == AssetTypeId.FuturesOption)
        {
            if (optionRoutes.StopIndividual(mapping.ContractId))
                liveRouter.Deactivate(mapping.ContractId);
            return;
        }

        liveRouter.Deactivate(mapping.ContractId);
    }
}
