using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketData;

public class FuturesContractIdParser
{
    public string ContractId { get; }
    public string Symbol { get; }
    public DateOnly MaturityDate { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesContractIdParser"/> class using the specified contract ID.
    /// </summary>
    /// <remarks>The <paramref name="contractId"/> is parsed to extract the symbol and the maturity date of
    /// the futures contract. The symbol is derived from the portion of the string preceding the date, and the maturity
    /// date is parsed from the last 8 characters.</remarks>
    /// <param name="contractId">The futures contract identifier, which must be a non-empty string with a minimum length of 9 characters. The
    /// identifier is expected to end with an 8-character date in the format "yyyyMMdd".</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="contractId"/> is null, empty, or consists only of whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="contractId"/> is less than 9 characters in length or if it does not contain a valid
    /// date in the expected format.</exception>
    [JsonConstructor]
    public FuturesContractIdParser(string contractId)
    {
        if (string.IsNullOrWhiteSpace(contractId))
            throw new ArgumentException("FuturesContractId: contractId is empty");
        if (contractId.Length < 9)
            throw new InvalidOperationException($"FuturesContractId: '{contractId}' length is less than 9");
        try
        {
            ContractId = contractId;
            var dateStart = contractId.Length - 8;
            var year = Convert.ToInt32(contractId.Substring(dateStart, 4));
            var month = Convert.ToInt32(contractId.Substring(dateStart + 4, 2));
            var day = Convert.ToInt32(contractId.Substring(dateStart + 6, 2));
            MaturityDate = new DateOnly(year, month, day);
            Symbol = contractId[..dateStart];
        }
        catch
        {
            throw new InvalidOperationException($"FuturesContractId: invalid contractId '{contractId}'");
        }
    }

    public FuturesContractId Id => new(ContractId, Symbol, MaturityDate);
    public override string ToString() => JsonConvert.SerializeObject(this);
}
