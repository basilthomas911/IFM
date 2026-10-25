using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IDatabentoMarketDataQueries
{
    /// <summary>Latest provider definition interval for all supported instruments in this dataset.</summary>
    IReadOnlyList<ContractDetail> GetDatasetDefinitions(TimeSpan? timeout = null)
        => throw new NotSupportedException("Dataset-wide definition discovery is not implemented by this provider.");

    OptionChainDefinitions GetChainDefinitions(
        OptionChainDefinitionRequest request,
        TimeSpan? timeout = null);

    uint ContractIdToInstrumentId(
        string contractId,
        TimeSpan? timeout = null);

    string InstrumentIdToContractId(
        uint instrumentId,
        TimeSpan? timeout = null);

    ContractDetail? GetContractDetail(
        string contractName,
        TimeSpan? timeout = null);

    IReadOnlyList<ContractDetail> GetContractDetails(
        string ticker,
        TimeSpan? timeout = null);

    IReadOnlyList<ContractDetail?> GetContractDetails(
        string[] contractNames,
        TimeSpan? timeout = null);

    DatabentoContractDetailsQueryResult TryGetContractDetails(
        string[] contractNames,
        TimeSpan? timeout = null)
    {
        try
        {
            return DatabentoContractDetailsQueryResult.Success(
                GetContractDetails(contractNames, timeout));
        }
        catch (DatabentoFeedTimeoutException exception)
        {
            return DatabentoContractDetailsQueryResult.Failure(
                DatabentoFeedStatus.Timeout,
                exception.Message);
        }
        catch (DatabentoFeedException exception)
        {
            return DatabentoContractDetailsQueryResult.Failure(
                exception.Status,
                exception.Message);
        }
    }
}
