using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;

/// <summary>
/// securities database constructor
/// </summary>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
///  <param name="logger"
public class SecuritiesDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<DbProvider> logger) 
    : ObjectDataRepository<SecuritiesDbContext>(connectionSettings[SecuritiesDbConnection], logger), ISecuritiesDbContext
{
    public const string SecuritiesDbConnection = "SecuritiesDbConnection";
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override IObjectRepository Database => this;

    public ISecuritiesDbReadContext DbReader => this;
    public ISecuritiesDbWriteContext DbWriter => this;

    static FuturesContractV2ReadModel MapToFuturesContract<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            description: e.GetString(1),
            symbol: e.GetString(2),
            localSymbol: e.GetString(3),
            securityType: e.GetString(4),
            currency: e.GetString(5),
            exchange: e.GetString(6),
            multiplier: e.GetString(7),
            lastTradeDate: e.GetDateOnly(8),
            currentlyTraded: e.GetBool(9)
        );

    static FuturesOptionContractReadModel MapToFuturesOptionContract<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new(
            contractId: e.GetString(0),
            description: e.GetString(1),
            symbol: e.GetString(2),
            localSymbol: e.GetString(3),
            securityType: e.GetString(4),
            currency: e.GetString(5),
            exchange: e.GetString(6),
            multiplier: e.GetString(7),
            contractMonth: e.GetDateOnly(8),
            strikePrice: e.GetDouble(9),
            optionType: e.GetString(10)
        );

    /// <summary>
    /// Insert a new futures contract into SecuritiesDb
    /// </summary>
    /// <param name="futuresContract">The futures contract to insert</param>
    /// <returns></returns>
    public async Task InsertFuturesContractAsync(FuturesContractV2ReadModel futuresContract)
        =>  await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.InsertFuturesContract)
            .SetParameters(new InsertFuturesContract(futuresContract.ContractId, futuresContract.Description, futuresContract.Symbol, futuresContract.LocalSymbol, futuresContract.SecurityType, futuresContract.Currency, futuresContract.Exchange, futuresContract.Multiplier, futuresContract.LastTradeDate, futuresContract.CurrentlyTraded))
            .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously inserts a collection of futures contracts into the database.
    /// </summary>
    /// <remarks>This method uses the database factory to execute an insert command for each futures contract
    /// in the provided collection. Ensure that the collection is not null and contains valid futures contract data to
    /// avoid exceptions during execution.</remarks>
    /// <param name="futuresContracts">A collection of <see cref="FuturesContractV2ReadModel"/> objects representing the futures contracts to be
    /// inserted. Each contract must have valid properties set, such as contract ID, description, symbol, and other
    /// relevant details.</param>
    /// <returns></returns>
    public async Task InsertFuturesContractsAsync(ICollection<FuturesContractV2ReadModel> futuresContracts)
        => await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.InsertFuturesContract)
            .SetParameters(futuresContracts.Select(e => new InsertFuturesContract(e.ContractId, e.Description, e.Symbol, e.LocalSymbol, e.SecurityType, e.Currency, e.Exchange, e.Multiplier, e.LastTradeDate, e.CurrentlyTraded)))
            .ExecuteCommandAsync();

    /// <summary>
    /// Update an existing futures contract in SecuritiesDb
    /// </summary>
    /// <param name="e">The ID of the futures contract to update</param>
    /// <param name="futuresContract">The futures contract to update</param>
    /// <returns></returns>
    public async Task UpdateFuturesContractAsync(FuturesContractId e, FuturesContractV2ReadModel futuresContract)
    {
        var db = _dbFactory.SecuritiesDb;
        List<object> queuedCommands = [
            db.Use(SecuritiesDbCql.DeleteFuturesContractById)
                .SetParameters(new DeleteFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
                .QueueCommand(),
            db.Use(SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(new InsertFuturesContract(futuresContract.ContractId, futuresContract.Description, futuresContract.Symbol, futuresContract.LocalSymbol, futuresContract.SecurityType, futuresContract.Currency, futuresContract.Exchange, futuresContract.Multiplier, futuresContract.LastTradeDate, futuresContract.CurrentlyTraded))
                .QueueCommand()];
        await db.ExecuteQueuedCommandsAsync(queuedCommands, false);
    }

    /// Delete a futures contract from SecuritiesDb by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures contract to delete</param>
    /// <returns></returns>
    public async Task DeleteFuturesContractAsync(string contractId)
        =>  await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.DeleteFuturesContract)
            .SetParameters(new DeleteFuturesContract(contractId))
            .ExecuteCommandAsync();

    /// <summary>
    /// Deletes a futures contract from the database asynchronously.
    /// </summary>
    /// <remarks>This method removes the specified futures contract from the database. Ensure that the
    /// provided  <paramref name="e"/> contains valid and complete information for the contract to be deleted.</remarks>
    /// <param name="e">The identifier of the futures contract to delete, including the contract ID, symbol, and maturity date.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteFuturesContractAsync(FuturesContractId e)
        => await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.DeleteFuturesContract)
            .SetParameters(new DeleteFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
            .ExecuteCommandAsync();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task DeleteCurrentlyTradedFuturesContractAsync(string symbol)
    {
        var db = _dbFactory.SecuritiesDb;
        var fc = await _dbFactory.SecuritiesDb
                .Use(SecuritiesDbCql.GetCurrentlyTradeFuturesContract)
                .SetParameters(new GetCurrentlyTradeFuturesContract(symbol))
                .ExecuteSingleAsync(MapToFuturesContract!);
        if (fc is not null)
            await db
                .Use(SecuritiesDbCql.DeleteFuturesContract)
                .SetParameters(new DeleteFuturesContractById(fc.ContractId, fc.Symbol, fc.LastTradeDate))
                .ExecuteCommandAsync();
    }

/// <summary>
    /// Get currently traded futures contract from the database 
    /// </summary>
    /// <returns>The futures contract with the specified ID</returns>
    public async Task<FuturesContractV2ReadModel?> GetCurrentlyTradedFuturesContractAsync(string symbol)
        => await _dbFactory.SecuritiesDb
                .Use(SecuritiesDbCql.GetCurrentlyTradeFuturesContract)
                .SetParameters(new GetCurrentlyTradeFuturesContract(symbol))
                .ExecuteSingleAsync(MapToFuturesContract!);

    /// <summary>
    /// Get currently traded futures contracts from the database 
    /// </summary>
    /// <returns>The futures contract with the specified ID</returns>
    public async Task<ICollection<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractsAsync(string symbol)
        => await _dbFactory.SecuritiesDb
                .Use(SecuritiesDbCql.GetCurrentlyTradeFuturesContracts)
                .SetParameters(new GetCurrentlyTradeFuturesContracts(symbol))
                .ExecuteQueryAsync(MapToFuturesContract!);

    /// <summary>
    /// Get a futures contract from the database by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures contract to retrieve</param>
    /// <returns>The futures contract with the specified ID</returns>
    public async Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(string contractId)
        => await _dbFactory.SecuritiesDb
                .Use(SecuritiesDbCql.GetFuturesContract)
                .SetParameters(new GetFuturesContract(contractId))
                .ExecuteSingleAsync<FuturesContractV2ReadModel>(MapToFuturesContract!);

    /// <summary>
    /// Retrieves a futures contract based on the specified contract identifier.
    /// </summary>
    /// <remarks>This method queries the database to retrieve details of a specific futures contract. Ensure
    /// that the provided  <paramref name="e"/> contains valid and complete information for the query to
    /// succeed.</remarks>
    /// <param name="e">The identifier of the futures contract, including the contract ID, symbol, and maturity date.</param>
    /// <returns>A <see cref="FuturesContractV2ReadModel"/> representing the futures contract if found; otherwise, <see
    /// langword="null"/>.</returns>
    public async Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(FuturesContractId e)
        => await _dbFactory.SecuritiesDb
                .Use(SecuritiesDbCql.GetFuturesContractById)
                .SetParameters(new GetFuturesContractById(e.ContractId, e.Symbol, e.MaturityDate))
                .ExecuteSingleAsync(MapToFuturesContract!);

    /// <summary>
    /// Get all futures contracts from the database
    /// </summary>
    /// <returns>A list of all futures contracts</returns>
    public async Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsAsync()
        => await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.GetFuturesContracts)
            .ExecuteQueryAsync(MapToFuturesContract!);

    /// <summary>
    /// Insert a new futures option contract into SecuritiesDb
    /// </summary>
    /// <param name="futuresOptionContract"></param>
    /// <returns></returns>
    public async Task InsertFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract)
        => await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.InsertFuturesOptionContract)
            .SetParameters(new InsertFuturesOptionContract(futuresOptionContract.ContractId, futuresOptionContract.Description, futuresOptionContract.Symbol, futuresOptionContract.LocalSymbol, futuresOptionContract.SecurityType, futuresOptionContract.Currency, futuresOptionContract.Exchange, futuresOptionContract.Multiplier, futuresOptionContract.ContractMonth, futuresOptionContract.StrikePrice, futuresOptionContract.OptionType))
            .ExecuteCommandAsync();

    /// <summary>
    /// Asynchronously inserts a collection of futures option contracts into the database.
    /// </summary>
    /// <remarks>This method uses the database factory to execute an asynchronous command that inserts the
    /// provided futures option contracts. Ensure that each contract in the collection has all required fields populated
    /// to avoid database errors.</remarks>
    /// <param name="futuresOptionContract">A collection of <see cref="FuturesOptionContractReadModel"/> objects representing the futures option contracts
    /// to be inserted. Each object must contain valid contract details such as contract ID, description, symbol, and
    /// other relevant properties.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InsertFuturesOptionContractsAsync(ICollection<FuturesOptionContractReadModel> futuresOptionContract)
        => await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.InsertFuturesOptionContract)
            .SetParameters(futuresOptionContract.Select(e => new InsertFuturesOptionContract(e.ContractId, e.Description, e.Symbol, e.LocalSymbol, e.SecurityType, e.Currency, e.Exchange, e.Multiplier, e.ContractMonth, e.StrikePrice, e.OptionType)))
            .ExecuteCommandAsync();

    /// <summary>
    /// Update an existing futures option contract in SecuritiesDb
    /// </summary>
    /// <param name="originalContractId"></param>
    /// <param name="futuresOptionContract"></param>
    /// <returns></returns>
    public async Task UpdateFuturesOptionContractAsync(string originalContractId, FuturesOptionContractReadModel futuresOptionContract)
    {
        var db = _dbFactory.SecuritiesDb;
        List<object> queuedCommands = [
            db.Use(SecuritiesDbCql.DeleteFuturesOptionContract)
                .SetParameters(new DeleteFuturesOptionContract(originalContractId))
                .QueueCommand(),
            db.Use(SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(new InsertFuturesOptionContract(futuresOptionContract.ContractId, futuresOptionContract.Description, futuresOptionContract.Symbol, futuresOptionContract.LocalSymbol, futuresOptionContract.SecurityType, futuresOptionContract.Currency, futuresOptionContract.Exchange, futuresOptionContract.Multiplier, futuresOptionContract.ContractMonth, futuresOptionContract.StrikePrice, futuresOptionContract.OptionType))
                .QueueCommand()
        ];
        await db.ExecuteQueuedCommandsAsync(queuedCommands, false);
    }

    /// <summary>
    /// Delete a futures option contract from SecuritiesDb by its ID
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task DeleteFuturesOptionContractAsync(string contractId )
        => await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.DeleteFuturesOptionContract)
            .SetParameters(new DeleteFuturesOptionContract(contractId))
            .ExecuteCommandAsync();

    /// <summary>
    /// Get a futures option contract from the database by its ID
    /// </summary>
    /// <param name="contractId">The ID of the futures option contract to retrieve</param>
    /// <returns>The futures option contract with the specified ID</returns>
    public async Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId)
        => await _dbFactory.SecuritiesDb
                .Use(SecuritiesDbCql.GetFuturesOptionContract)
                .SetParameters(new GetFuturesOptionContract(contractId))
                .ExecuteSingleAsync<FuturesOptionContractReadModel>(MapToFuturesOptionContract!);

    /// <summary>
    /// Get all futures option contracts from the database
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns>A list of all futures option contracts</returns>
    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync(string symbol)
        => [.. (await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.GetFuturesOptionContracts)
            .ExecuteQueryAsync(MapToFuturesOptionContract!)).Where(e => e.Symbol == symbol)];

    /// <summary>
    /// Asynchronously retrieves a collection of futures option contracts.
    /// </summary>
    /// <remarks>This method queries the database to obtain the current futures option contracts and maps them
    /// to the <see cref="FuturesOptionContractReadModel"/> type. The returned collection may be empty  if no contracts
    /// are available.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <see
    /// cref="FuturesOptionContractReadModel"/> representing the futures option contracts.</returns>
    public async Task<ICollection<FuturesOptionContractReadModel>> GetFuturesOptionContractsAsync()
        => await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.GetFuturesOptionContracts)
            .ExecuteQueryAsync(MapToFuturesOptionContract!);

    /// <summary>
    /// Get futures contracts from the database by a list of contract IDs by symbol
    /// </summary>
    /// <param name="contractIds">The list of contract IDs to retrieve</param>
    /// <param name="symbol">The symbol of the futures contracts to retrieve</param>
    /// <returns>A list of futures contracts with the specified IDs</returns>
    public async Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsByIdsAsync(ICollection<string> contractIds, string symbol)
        =>  await _dbFactory.SecuritiesDb
            .Use(SecuritiesDbCql.GetFuturesContractsByIds)
            .SetParameters(new GetFuturesContractsByIds(contractIds, symbol))
            .ExecuteQueryAsync(MapToFuturesContract!);

    /// <summary>
    /// Get futures contracts from the database by symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public async Task<ICollection<FuturesContractV2ReadModel>> GetFuturesContractsBySymbolAsync(string symbol)
       => await _dbFactory.SecuritiesDb
           .Use(SecuritiesDbCql.GetFuturesContractsBySymbol)
           .SetParameters(new GetFuturesContractsBySymbol(symbol))
           .ExecuteQueryAsync(MapToFuturesContract!);

}
