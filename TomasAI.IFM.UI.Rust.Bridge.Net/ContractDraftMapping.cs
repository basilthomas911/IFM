using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Maps explicit editor fields with the same provider and identity rules as the existing contract controls.</summary>
public static class ContractDraftMapping
{
    static string Field(ContractEditorIntent input,string name)=>input.Fields.GetValueOrDefault(name)?.Trim()??"";
    static DateOnly Date(ContractEditorIntent input,string name)=>DateOnly.ParseExact(Field(input,name),"yyyy-MM-dd",CultureInfo.InvariantCulture);
    static bool Flag(ContractEditorIntent input,string name)=>bool.Parse(Field(input,name));
    public static FuturesContractV3ReadModel Futures(ContractEditorIntent input,FuturesContractV3ReadModel? original)
    {
        if(original is {SchemaVersion:>0}) return original with{Description=Field(input,"description"),OnTheRun=Flag(input,"onTheRun")};
        var symbol=Field(input,"symbol");var date=Date(input,"lastTradeDate");
        return (original??new FuturesContractV3ReadModel()) with{
            ContractId=$"{symbol}{date:yyyyMMdd}",Description=Field(input,"description"),Symbol=symbol,
            LocalSymbol=Field(input,"localSymbol"),SecurityType=Field(input,"securityType"),Currency=Field(input,"currency"),
            Exchange=Field(input,"exchange"),Multiplier=Field(input,"multiplier"),LastTradeDate=date,
            OnTheRun=Flag(input,"onTheRun"),Rollover=Flag(input,"rollover")};
    }
    public static FuturesOptionContractReadModel Option(ContractEditorIntent input,FuturesOptionContractReadModel? original)
    {
        if(original is {SchemaVersion:>0}) return original with{Description=Field(input,"description")};
        var symbol=Field(input,"symbol");var date=Date(input,"contractMonth");var optionType=Field(input,"optionType");
        if(!FuturesOptionContractId.TryParseStrike(Field(input,"strikePrice").AsSpan(),out var strike))throw new ArgumentException("Enter an exact positive strike");
        var right=optionType.StartsWith("C",StringComparison.OrdinalIgnoreCase)?OptionType.Call:
            optionType.StartsWith("P",StringComparison.OrdinalIgnoreCase)?OptionType.Put:throw new ArgumentException("Select Call or Put");
        if(original is not null && (original.Symbol!=symbol || original.ContractMonth!=date ||
            !string.Equals(original.OptionType,optionType,StringComparison.Ordinal) || original.GetExactStrikePrice()!=strike))
            throw new ArgumentException("Contract identity cannot be changed. Add a new contract instead.");
        return (original??new FuturesOptionContractReadModel()) with{
            ContractId=original?.ContractId??FuturesOptionContractId.Create(symbol,date,right,strike),Description=Field(input,"description"),Symbol=symbol,
            LocalSymbol=Field(input,"localSymbol"),SecurityType=Field(input,"securityType"),Currency=Field(input,"currency"),
            Exchange=Field(input,"exchange"),Multiplier=Field(input,"multiplier"),ContractMonth=date,
            StrikePrice=(double)strike,StrikePriceDecimal=strike,OptionType=optionType};
    }
}
