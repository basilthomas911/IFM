using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.CommandParameters;

/// <summary>
/// Represents the parameters required to remove a yield curve rate for a specific value (trading) date.
/// </summary>
/// <param name="ValueDate">The target value (trading) date of the yield curve rate to remove.</param>
/// <param name="Overwrite">True to force/overwrite removal where applicable; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the remove yield curve rate operation. Used to indicate specific error conditions or statuses.</param>
public record RemoveYieldCurveRateParameter(DateOnly ValueDate, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}
