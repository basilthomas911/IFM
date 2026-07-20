using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.CommandParameters;

/// <summary>
/// Represents the parameters required to change a yield curve rate, including the rate details and an optional overwrite flag.
/// </summary>
/// <param name="YieldCurveRate">The yield curve rate details to be changed. Cannot be null.</param>
/// <param name="Overwrite">True to overwrite an existing rate with the same entity identifier; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the change yield curve rate operation. Used to indicate specific error conditions or statuses.</param>
public record ChangeYieldCurveRateParameter(YieldCurveRateReadModel YieldCurveRate, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}
