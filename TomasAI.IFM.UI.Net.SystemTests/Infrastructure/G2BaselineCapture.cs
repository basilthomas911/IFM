using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed record G2BaselineSnapshot(
    DateTimeOffset CapturedUtc,
    string RunPrefix,
    DateOnly ValueDate,
    DateOnly ImportDate,
    string[] ImportCountryCodes,
    FuturesContractV3ReadModel[] RunOwnedFuturesContracts,
    FuturesOptionContractReadModel[] RunOwnedFuturesOptions,
    FuturesContractV3ReadModel? SecuritiesFixtureContract,
    FuturesOptionContractReadModel? SecuritiesFixtureOption,
    YieldCurveRateReadModel[] YieldCurveManualDateRows,
    YieldCurveRateReadModel[] YieldCurveImportDateRows,
    EconomicCalendarReadModel[] EconomicCalendarManualDateRows,
    IReadOnlyDictionary<string, EconomicCalendarReadModel[]> EconomicCalendarImportDateRows,
    LookupTypeReadModel[] RunOwnedLookupTypes,
    FundReadModel? DesignatedFund,
    FundBalanceReadModel? DesignatedFundBalance,
    FundTransactionReadModel[] DesignatedFundTransactions,
    FundOrderReadModel[] DesignatedFundOrders,
    FundOrderTradeReadModel[] DesignatedFundTrades);

public static class G2BaselineCapture
{
    public static async Task<G2BaselineSnapshot> CaptureAsync(
        G0QuerySession queries,
        G2Configuration configuration,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(configuration);

        var valueDate = RequireValue(
            await queries.MarketData.GetValueDateAsync().WaitAsync(timeout, cancellationToken),
            "application value date").Value;
        var contracts = RequireValue(
            await queries.MarketData.GetFuturesContractsAsync().WaitAsync(timeout, cancellationToken),
            "futures contracts");
        var options = RequireValue(
            await queries.MarketData.GetFuturesOptionContractsAsync(configuration.SecuritiesSymbol)
                .WaitAsync(timeout, cancellationToken),
            $"{configuration.SecuritiesSymbol} futures option contracts");
        var yieldCurve = RequireValue(
            await queries.MarketData.GetYieldCurveRatesAsync(configuration.ImportDate, configuration.ImportDate)
                .WaitAsync(timeout, cancellationToken),
            "yield-curve import-date baseline");
        var manualYieldCurve = RequireValue(
            await queries.MarketData.GetYieldCurveRatesAsync(
                    configuration.YieldCurveManualDate,
                    configuration.YieldCurveManualDate)
                .WaitAsync(timeout, cancellationToken),
            "manual yield-curve fixture baseline");

        Dictionary<string, EconomicCalendarReadModel[]> calendars = new(StringComparer.Ordinal);
        var calendarDate = DateTime.SpecifyKind(
            configuration.ImportDate.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);
        foreach (var countryCode in configuration.ImportCountryCodes)
        {
            calendars[countryCode] = RequireValue(
                await queries.MarketData.GetEconomicCalendarsAsync(
                        calendarDate,
                        EconomicCalendarViewType.Today,
                        countryCode)
                    .WaitAsync(timeout, cancellationToken),
                $"economic-calendar baseline for {countryCode}");
        }
        var manualCalendarDate = DateTime.SpecifyKind(
            configuration.EconomicCalendarManualDate.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);
        var manualCalendars = RequireValue(
            await queries.MarketData.GetEconomicCalendarsAsync(
                    manualCalendarDate,
                    EconomicCalendarViewType.Today,
                    configuration.ImportCountryCodes[0])
                .WaitAsync(timeout, cancellationToken),
            $"manual economic-calendar baseline for {configuration.ImportCountryCodes[0]}");

        var lookupTypes = RequireValue(
            await queries.Reference.GetLookupTypesAsync().WaitAsync(timeout, cancellationToken),
            "lookup types");
        var funds = RequireValue(
            await queries.Fund.GetFundsAsync().WaitAsync(timeout, cancellationToken),
            "funds");
        var designatedFund = funds.SingleOrDefault(fund => string.Equals(
            fund.Name,
            configuration.FundFixtureName,
            StringComparison.Ordinal));

        FundBalanceReadModel? balance = null;
        FundTransactionReadModel[] transactions = [];
        FundOrderReadModel[] orders = [];
        FundOrderTradeReadModel[] trades = [];
        if (designatedFund is not null)
        {
            balance = RequireValue(
                await queries.Fund.GetFundBalanceAsync(designatedFund.FundId)
                    .WaitAsync(timeout, cancellationToken),
                "designated G2 fund balance");
            transactions = RequireValue(
                await queries.Fund.GetFundTransactionsAsync(
                        designatedFund.FundId,
                        valueDate,
                        valueDate)
                    .WaitAsync(timeout, cancellationToken),
                "designated G2 fund transactions");
            orders = RequireValue(
                    await queries.Fund.GetFundOrdersAsync().WaitAsync(timeout, cancellationToken),
                    "fund orders")
                .Where(order => order.FundId == designatedFund.FundId)
                .ToArray();
            trades = RequireValue(
                    await queries.Fund.GetFundOrderTradesAsync().WaitAsync(timeout, cancellationToken),
                    "fund order trades")
                .Where(trade => trade.FundId == designatedFund.FundId)
                .ToArray();
        }

        return new G2BaselineSnapshot(
            DateTimeOffset.UtcNow,
            configuration.RunPrefix,
            valueDate,
            configuration.ImportDate,
            configuration.ImportCountryCodes,
            contracts.Where(contract =>
                    IsRunOwned(contract.ContractId, configuration.RunPrefix)
                    || IsRunOwned(contract.Description, configuration.RunPrefix))
                .ToArray(),
            options.Where(option =>
                    IsRunOwned(option.ContractId, configuration.RunPrefix)
                    || IsRunOwned(option.Description, configuration.RunPrefix))
                .ToArray(),
            contracts.SingleOrDefault(contract => string.Equals(
                contract.ContractId,
                configuration.SecuritiesFuturesContractId,
                StringComparison.Ordinal)),
            options.SingleOrDefault(option => string.Equals(
                option.ContractId,
                configuration.SecuritiesOptionContractId,
                StringComparison.Ordinal)),
            manualYieldCurve,
            yieldCurve,
            manualCalendars,
            calendars,
            lookupTypes.Where(lookup =>
                    IsRunOwned(lookup.LookupTypeName, configuration.RunPrefix)
                    || IsRunOwned(lookup.ShortCode, configuration.RunPrefix)
                    || IsRunOwned(lookup.Description, configuration.RunPrefix))
                .ToArray(),
            designatedFund,
            balance,
            transactions,
            orders,
            trades);
    }

    static T RequireValue<T>(ServiceResult<T> result, string queryName)
        where T : class
    {
        if (!result.Success || result.Value is null)
            throw new G0DependencyException(
                $"Typed {queryName} query failed: code={result.ErrorCode}; message={result.ErrorMessage}");
        return result.Value;
    }

    static bool IsRunOwned(string? value, string runPrefix)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(runPrefix, StringComparison.OrdinalIgnoreCase);
}
