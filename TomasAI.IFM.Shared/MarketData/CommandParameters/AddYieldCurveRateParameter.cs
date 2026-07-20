using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.CommandParameters;

/// <summary>
/// Represents the parameters required to add a yield curve rate, including the rate details and an optional overwrite flag.
/// </summary>
/// <param name="YieldCurveRate">The yield curve rate details to be added. Cannot be null.</param>
/// <param name="Overwrite">True to overwrite an existing rate with the same entity identifier; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the add yield curve rate operation. Used to indicate specific error conditions or statuses.</param>
public record AddYieldCurveRateParameter(YieldCurveRateReadModel YieldCurveRate, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}
