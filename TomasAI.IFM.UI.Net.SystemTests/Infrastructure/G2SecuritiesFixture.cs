using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

/// <summary>
/// Exact, run-owned securities fixture and the presentation indexes exposed by the live editor lookups.
/// </summary>
public sealed record G2SecuritiesFixture(
    string Symbol,
    DateOnly MaturityDate,
    int StrikePrice,
    string FuturesContractId,
    string OptionContractId,
    string FuturesAddedDescription,
    string FuturesChangedDescription,
    string OptionAddedDescription,
    string OptionChangedDescription,
    string FuturesDefinitionDescription,
    string OptionDefinitionDescription,
    int SymbolIndex,
    int FuturesSecurityTypeIndex,
    int OptionSecurityTypeIndex,
    int CurrencyIndex,
    int ExchangeIndex,
    int MultiplierIndex,
    int CallOptionTypeIndex,
    string Currency,
    string Exchange,
    string Multiplier)
{
    public static async Task<G2SecuritiesFixture> CreateAsync(
        G0QuerySession queries,
        G2Configuration configuration,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(configuration);

        var contracts = Require(
            await queries.MarketData.GetFuturesContractsAsync().WaitAsync(timeout, cancellationToken),
            "futures contracts");
        var underlying = contracts
            .Where(contract => string.Equals(contract.Symbol, configuration.SecuritiesSymbol, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(contract => contract.CurrentlyTraded)
            .ThenByDescending(contract => contract.LastTradeDate)
            .FirstOrDefault()
            ?? throw new G0DependencyException(
                $"No existing {configuration.SecuritiesSymbol} futures contract can supply the fixture's validated exchange, currency, and multiplier.");

        var symbols = await LookupAsync(queries, "Symbol", timeout, cancellationToken);
        var securityTypes = await LookupAsync(queries, "SecurityType", timeout, cancellationToken);
        var currencies = await LookupAsync(queries, "Currency", timeout, cancellationToken);
        var exchanges = await LookupAsync(queries, "Exchange", timeout, cancellationToken);
        var multipliers = await LookupAsync(queries, "Multiplier", timeout, cancellationToken);
        var optionTypes = await LookupAsync(queries, "OptionType", timeout, cancellationToken);
        var marketDataDefinitions = Require(
                await queries.Reference.GetMarketDataDefinitionTypesAsync()
                    .WaitAsync(timeout, cancellationToken),
                "MarketDataDefinitionType lookup")
            .ToArray();
        var currencyIndex = IndexOf(currencies, underlying.Currency, "Currency");
        var exchangeIndex = IndexOfExchange(exchanges, underlying.Exchange);
        var multiplierIndex = IndexOfOrFirst(multipliers, underlying.Multiplier, "Multiplier");

        return new G2SecuritiesFixture(
            configuration.SecuritiesSymbol,
            configuration.SecuritiesMaturityDate,
            configuration.SecuritiesOptionStrike,
            configuration.SecuritiesFuturesContractId,
            configuration.SecuritiesOptionContractId,
            $"{configuration.RunPrefix} futures added",
            $"{configuration.RunPrefix} futures changed",
            $"{configuration.RunPrefix} option added",
            $"{configuration.RunPrefix} option changed",
            DescriptionOf(marketDataDefinitions, "FuturesContract", "MarketDataDefinitionType"),
            DescriptionOf(marketDataDefinitions, "FuturesOptionContract", "MarketDataDefinitionType"),
            IndexOf(symbols, configuration.SecuritiesSymbol, "Symbol"),
            IndexOf(securityTypes, SecurityType.FUT.ToStringFast(), "SecurityType"),
            IndexOf(securityTypes, SecurityType.FOP.ToStringFast(), "SecurityType"),
            currencyIndex,
            exchangeIndex,
            multiplierIndex,
            IndexOf(optionTypes, OptionType.Call.ToStringFast(), "OptionType"),
            currencies[currencyIndex].ShortCode,
            exchanges[exchangeIndex].ShortCode,
            multipliers[multiplierIndex].ShortCode);
    }

    static async Task<LookupTypeReadModel[]> LookupAsync(
        G0QuerySession queries,
        string lookupTypeName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => Require(
                await queries.Reference.GetLookupTypesAsync(lookupTypeName)
                    .WaitAsync(timeout, cancellationToken),
                $"{lookupTypeName} lookup")
            .ToArray();

    static int IndexOf(
        IReadOnlyList<LookupTypeReadModel> values,
        string shortCode,
        string lookupTypeName)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index].ShortCode, shortCode, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        throw new G0DependencyException(
            $"The {lookupTypeName} lookup does not contain required short code '{shortCode}'.");
    }

    static int IndexOfExchange(IReadOnlyList<LookupTypeReadModel> values, string venueCode)
    {
        var candidates = venueCode.StartsWith('X') && venueCode.Length > 1
            ? new[] { venueCode, venueCode[1..] }
            : new[] { venueCode };
        foreach (var candidate in candidates)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index].ShortCode, candidate, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
        }
        throw new G0DependencyException(
            $"The Exchange lookup does not contain venue '{venueCode}' or its normalized UI code.");
    }

    static int IndexOfOrFirst(
        IReadOnlyList<LookupTypeReadModel> values,
        string preferredShortCode,
        string lookupTypeName)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index].ShortCode, preferredShortCode, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        if (values.Count > 0)
            return 0;
        throw new G0DependencyException($"The {lookupTypeName} lookup is empty.");
    }

    static string DescriptionOf(
        IReadOnlyList<LookupTypeReadModel> values,
        string shortCode,
        string lookupTypeName)
        => values.SingleOrDefault(value => string.Equals(
                value.ShortCode,
                shortCode,
                StringComparison.OrdinalIgnoreCase))?.Description
            ?? throw new G0DependencyException(
                $"The {lookupTypeName} lookup does not contain required short code '{shortCode}'.");

    static T Require<T>(ServiceResult<T> result, string queryName)
        where T : class
    {
        if (!result.Success || result.Value is null)
            throw new G0DependencyException(
                $"Typed {queryName} query failed: code={result.ErrorCode}; message={result.ErrorMessage}");
        return result.Value;
    }
}
