using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

public static class FuturesAdxSignalIdExtensions
{
    /// <summary>
    /// Validates the <see cref="FuturesAdxSignalId"/> object and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>
    /// This method checks that the <c>ContractId</c> is not empty, the <c>ValueDate</c> is within valid range,
    /// and the <c>Timestamp</c> is within valid range. If any property is invalid, a corresponding <see
    /// cref="ValidationError"/> is added to the <paramref name="validationErrors"/> list with a message prefixed by the
    /// <paramref name="commandName"/>.
    /// </remarks>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="futuresAdxSignalId">The <see cref="FuturesAdxSignalId"/> object to validate. The <c>ContractId</c> must not be empty,
    /// <c>ValueDate</c> must be within valid range, and <c>Timestamp</c> must be within valid range.</param>
    /// <param name="commandName">The name of the command being validated, used to prefix error messages for context.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any new validation errors found.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing public concrete validation collection contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing public concrete validation collection contract.")]
    public static List<ValidationError> ValidateFuturesAdxSignalId(this List<ValidationError> validationErrors, FuturesAdxSignalId futuresAdxSignalId, string commandName)
    {
        ArgumentNullException.ThrowIfNull(futuresAdxSignalId);
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (string.IsNullOrEmpty(futuresAdxSignalId.ContractId))
            validationErrors.Add(new($"{commandName}.ContractId is required"));
        if (futuresAdxSignalId.ValueDate == DateOnly.MinValue)
            validationErrors.Add(new($"{commandName}.ValueDate must be greater than DateOnly.MinValue"));
        if (futuresAdxSignalId.ValueDate == DateOnly.MaxValue)
            validationErrors.Add(new($"{commandName}.ValueDate must be less than DateOnly.MaxValue"));
        if (futuresAdxSignalId.TimePeriod == TimeFrameType.None)
            validationErrors.Add(new($"{commandName}.TimePeriod is invalid"));
        if (futuresAdxSignalId.PeriodLength <= 0)
            validationErrors.Add(new($"{commandName}.PeriodLength must be a positive integer   "));
        if (futuresAdxSignalId.Timestamp == TimeOnly.MinValue)
            validationErrors.Add(new($"{commandName}.Timestamp must be greater than TimeOnly.MinValue"));
        if (futuresAdxSignalId.Timestamp == TimeOnly.MaxValue)
            validationErrors.Add(new($"{commandName}.Timestamp must be less than TimeOnly.MaxValue"));
        return validationErrors;
    }
}

