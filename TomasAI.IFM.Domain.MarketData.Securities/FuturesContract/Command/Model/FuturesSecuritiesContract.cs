using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Model;

/// <summary>
/// Represents a futures contract, including its identifying details, trading information, and metadata.
/// </summary>
/// <remarks>A futures contract is a standardized legal agreement to buy or sell a specific financial instrument
/// or commodity  at a predetermined price at a specified time in the future. This class encapsulates the key attributes
/// of a  futures contract, such as its unique identifier, symbol, exchange, and last trade date.</remarks>
/// <param name="contractId"></param>
/// <param name="description"></param>
/// <param name="symbol"></param>
/// <param name="localSymbol"></param>
/// <param name="secType"></param>
/// <param name="currency"></param>
/// <param name="exchange"></param>
/// <param name="multiplier"></param>
/// <param name="lastTradeDate"></param>
/// <param name="currentlyTraded"></param>
public class FuturesSecuritiesContract(
    string contractId,
    string description,
    string symbol,
    string localSymbol,
    string secType,
    string currency,
    string exchange,
    string multiplier,
    DateOnly lastTradeDate,
    bool currentlyTraded)
{

    // public properties...
    public virtual string ContractId { get; } = contractId;
    public string Description { get; } = description;
    public string Symbol { get; } = symbol;
    public string LocalSymbol { get; } = localSymbol;
    public string SecurityType { get; } = secType;
    public string Currency { get; } = currency;
    public string Exchange { get; } = exchange;
    public string Multiplier { get; } = multiplier;
    public DateOnly LastTradeDate { get; } = lastTradeDate;
    public bool CurrentlyTraded { get; } = currentlyTraded;

    public FuturesSecuritiesContract(FuturesContractV2ReadModel model)
        :this(model.ContractId, model.Description, model.Symbol, model.LocalSymbol, model.SecurityType, model.Currency,
             model.Exchange, model.Multiplier, model.LastTradeDate, model.CurrentlyTraded)
    {
    }

    public FuturesContractV2ReadModel ToViewModel()
        => new (
            contractId: ContractId,
            description: Description,
            symbol: Symbol,
            securityType: SecurityType,
            lastTradeDate: LastTradeDate,
            multiplier: Multiplier,
            exchange: Exchange,
            currency: Currency,
            localSymbol: LocalSymbol,
            currentlyTraded: CurrentlyTraded
        );
}

