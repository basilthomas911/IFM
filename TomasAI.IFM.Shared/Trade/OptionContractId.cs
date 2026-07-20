using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade;

public partial class OptionContractId
{
    string _contractId;

    public static OptionContractId Create(string contractId) => new (contractId);

    [JsonConstructor]
    private OptionContractId(string contractId) => ParseContractId(contractId);

    public string Symbol { get; private set; }
    public DateTime MaturityDate { get; private set; }
    public OptionType OptionType { get; private set; }
    public int StrikePrice { get; private set; }

    public override string ToString() => _contractId;

    /// <summary>
    /// parse contract id into field components
    /// </summary>
    /// <param name="contractId"></param>
    void ParseContractId(string contractId)
    {
        if (string.IsNullOrWhiteSpace(contractId))
            throw new ArgumentNullException(nameof(contractId));
        _contractId = contractId;
        var regex = MyRegex();
        var matches = regex.Matches(contractId);
        if (matches.Count != 4)
            throw new InvalidOperationException($"OptionContractId.ParseContractId: invalid contract id '{contractId}'");
        SetSymbol(matches[0]);
        SetMaturityDate(matches[1]);
        SetOptionType(matches[2]);
        SetStrikePrice(matches[3]);
    }


    private void SetSymbol(Match symbolMatch) => Symbol = symbolMatch.Value;

    private void SetMaturityDate(Match maturityDateMatch)
    {
        try
        {
            MaturityDate = new DateTime(
                year: int.Parse(maturityDateMatch.Value.Substring(0, 4)),
                month: int.Parse(maturityDateMatch.Value.Substring(4, 2)),
                day: int.Parse(maturityDateMatch.Value.Substring(6, 2)));
        }
        catch 
        {
            throw new InvalidOperationException($"OptionContractId.SetMaturityDate: invalid maturity date '{maturityDateMatch.Value}'");
        }
    }

    private void SetOptionType(Match optionTypeMatch)
    {
        switch(optionTypeMatch.Value.ToUpper())
        {
            case "C":
                OptionType = OptionType.Call;
                break;
            case "P":
                OptionType = OptionType.Put;
                break;
            default:
                throw new InvalidOperationException($"OptionContractId.SetOptionType: invalid option type '{optionTypeMatch.Value}'");
        }
    }

    private void SetStrikePrice(Match strikePriceMatch)
    {
        try
        {
            StrikePrice = int.Parse(strikePriceMatch.Value);
        }
        catch
        {
            throw new InvalidOperationException($"OptionContractId.SetStrikePrice: invalid strike price '{strikePriceMatch.Value}'");
        }
    }

    public static  Regex MyRegex() => new Regex("([a-zA-Z]+)|([0-9]{8})|([a-zA-Z]{1})|([0-9]+)", RegexOptions.Singleline);
}
