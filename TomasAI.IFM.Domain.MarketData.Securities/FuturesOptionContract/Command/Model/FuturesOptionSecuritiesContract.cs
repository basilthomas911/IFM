using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

/// <summary>
/// Represents a futures option contract, including details such as the symbol, contract month, strike price, and option
/// type.
/// </summary>
/// <remarks>This class provides a comprehensive representation of a futures option contract, including its unique
/// identifier,  descriptive details, and associated financial attributes. It supports conversion to and from a view
/// model representation.</remarks>
public class FuturesOptionSecuritiesContract 
{
    string _description;
    string _symbol;
    string _secType;
    string _multiplier;
    string _exchange;
    string _currency;
    string _localSymbol;
    DateOnly _contractMonth;
    double _strikePrice;
    string _optionType;

    public string ContractId => $"{_symbol}{_contractMonth:yyyyMMdd}{_optionType.Substring(0, 1)}{_strikePrice:####}";
    public string Description => _description;
    public string Symbol => _symbol;
    public string LocalSymbol => _localSymbol;
    public string SecurityType => _secType;
    public string Currency => _currency;
    public string Exchange => _exchange;
    public string Multiplier => _multiplier;
    public DateOnly ContractMonth => _contractMonth;
    public double StrikePrice => _strikePrice;
    public string OptionType => _optionType;

    public FuturesOptionSecuritiesContract(
        string description,
        string symbol,
        string localSymbol,
        string secType,
        string currency,
        string exchange,
        string multiplier,
        DateOnly contractMonth,
        double strikePrice,
        string optionType)
    {
        _symbol = symbol;
        _secType = secType;
        _contractMonth = contractMonth;
        _strikePrice = strikePrice;
        _optionType = optionType;
        _multiplier = multiplier;
        _exchange = exchange;
        _currency = currency;
        _localSymbol = localSymbol;
        _description = description;
    }

    public FuturesOptionSecuritiesContract(FuturesOptionContractReadModel model)
        :this(model.Description, model.Symbol, model.LocalSymbol, model.SecurityType, model.Currency,
             model.Exchange, model.Multiplier, model.ContractMonth, model.StrikePrice, model.OptionType)
    {
    }

    public FuturesOptionContractReadModel ToViewModel()
        => new (
            contractId: ContractId,
            symbol: Symbol,
            localSymbol: LocalSymbol,
            securityType: SecurityType,
            currency: Currency,
            exchange: Exchange,
            multiplier: Multiplier,
            contractMonth: ContractMonth,
            optionType: OptionType,
            strikePrice: StrikePrice,
            description: Description
        );
}
