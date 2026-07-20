using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.CommandParameters;

/// <summary>
/// Represents the parameters required to import yield curve rates, including the import date, yield curve rate details, and an associated error code.
/// </summary>
/// <param name="ImportDate">The date associated with the imported yield curve rates.</param>
/// <param name="YieldCurveRates">The collection of yield curve rate view models to import. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the import operation. Used to indicate specific error conditions or statuses.</param>
public record ImportYieldCurveRatesParameter(
    DateTime ImportDate,
    YieldCurveRateReadModel[] YieldCurveRates,
    int ErrorCode)
    : ICommandParameter
{
}
