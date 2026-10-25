using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to acquire and import yield curve rates for a date.
/// </summary>
/// <param name="ImportDate">The date associated with the imported yield curve rates.</param>
/// <param name="ErrorCode">The error code associated with the import operation. Used to indicate specific error conditions or statuses.</param>
public record ImportYieldCurveRatesParameter(
    DateTime ImportDate,
    int ErrorCode)
    : ICommandParameter
{
}
