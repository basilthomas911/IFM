using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

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
    readonly FuturesOptionContractReadModel? _source;
    readonly string _contractId;
    readonly string _description;
    readonly string _symbol;
    readonly string _secType;
    readonly string _multiplier;
    readonly string _exchange;
    readonly string _currency;
    readonly string _localSymbol;
    readonly DateOnly _contractMonth;
    readonly double _strikePrice;
    readonly string _optionType;

    public string ContractId => _contractId;
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
        var legacy = new FuturesOptionContractReadModel { StrikePrice = strikePrice };
        _contractId = FuturesOptionContractId.Create(symbol, contractMonth,
            optionType switch
            {
                "Call" => TomasAI.IFM.Domain.MarketData.Shared.OptionType.Call,
                "Put" => TomasAI.IFM.Domain.MarketData.Shared.OptionType.Put,
                _ => throw new ArgumentException("Option type must be Call or Put.", nameof(optionType))
            }, legacy.GetExactStrikePrice());
    }

    public FuturesOptionSecuritiesContract(FuturesOptionContractReadModel model)
        :this(model.Description, model.Symbol, model.LocalSymbol, model.SecurityType, model.Currency,
             model.Exchange, model.Multiplier, model.ContractMonth, model.StrikePrice, model.OptionType)
    {
        _source = model;
        _contractId = model.ContractId;
    }

    public FuturesOptionContractReadModel ToViewModel()
        => _source ?? new (
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
