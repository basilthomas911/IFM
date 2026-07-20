using System.Text;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.Domain;

public static class ValidationErrorsExtension
{
    /// <summary>
    /// Throws a <see cref="CommandValidationException"/> if the provided list of validation errors is not empty.
    /// </summary>
    /// <remarks>Use this method to enforce validation rules by throwing an exception when any validation
    /// errors are detected. If the list of errors is empty, the method simply returns the original list.</remarks>
    /// <param name="errors">The list of <see cref="ValidationError"/> objects to evaluate. Must not be null.</param>
    /// <param name="errorCode">The error code to include in the exception if validation errors are present.</param>
    /// <returns>The original list of validation errors if no exception is thrown.</returns>
    /// <exception cref="CommandValidationException">Thrown if <paramref name="errors"/> contains one or more validation errors. The exception message will include
    /// the error messages from the list.</exception>
    public static List<ValidationError> ThrowCommandValidationExceptionOnAnyError(this List<ValidationError> errors, int errorCode)
    {
        if (errors != null && errors.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var e in errors)
                sb.AppendLine(e.ErrorMessage);
            throw new CommandValidationException(errorCode, $"{sb}");
        }
        return errors!;
    }

    /// <summary>
    /// Validates the specified <paramref name="commandId"/> and adds a validation error to the  <paramref
    /// name="validationErrors"/> list if the <paramref name="commandId"/> is empty.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to which any detected errors will be added.</param>
    /// <param name="commandId">The unique identifier of the command to validate. Must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="commandName">The name of the command, used in the validation error message if an error is detected.</param>
    /// <returns>The updated list of validation errors, including any new errors detected during validation.</returns>
    public static List<ValidationError> ValidateCommandId(this List<ValidationError> validationErrors, Guid commandId, string commandName)
    {
        if (commandId == Guid.Empty)
            validationErrors.Add(new ($"{commandName}.CommandId is empty"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified <paramref name="fundId"/> and adds a validation error to the <paramref
    /// name="validationErrors"/> list if the <paramref name="fundId"/> is invalid.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to which any new errors will be added.</param>
    /// <param name="fundId">The fund identifier to validate. Must be greater than 0.</param>
    /// <param name="commandName">The name of the command being validated, used to provide context in the error message.</param>
    /// <returns>The updated list of validation errors, including any new errors added during validation.</returns>
    public static List<ValidationError> ValidateFundId(this List<ValidationError> validationErrors, int fundId, string commandName)
    {
        if (fundId < 1)
            validationErrors.Add(new ($"{commandName}.FundId must be > 0"));
        return validationErrors;
    }

    

    
    /// <summary>
    /// Validates the specified order date and adds a validation error to the collection if the date is invalid.
    /// </summary>
    /// <param name="validationErrors">The collection of validation errors to which any detected errors will be added.</param>
    /// <param name="orderDate">The order date to validate. Must not be <see cref="DateTime.MinValue"/> or <see cref="DateTime.MaxValue"/>.</param>
    /// <param name="commandName">The name of the command associated with the order, used to identify the source of the validation error.</param>
    /// <returns>The updated collection of validation errors, including any errors detected during the validation of the order
    /// date.</returns>
    public static List<ValidationError> ValidateOrderDate(this List<ValidationError> validationErrors, DateTime orderDate, string commandName)
    {
        if (orderDate == DateTime.MinValue || orderDate == DateTime.MaxValue)
            validationErrors.Add(new ($"{commandName}.OrderDate is invalid"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified trade date and adds a validation error to the list if the date is invalid.
    /// </summary>
    /// <remarks>A trade date is considered invalid if it is equal to <see cref="DateOnly.MinValue"/> or <see
    /// cref="DateOnly.MaxValue"/>. In such cases, a validation error message is added to the <paramref
    /// name="validationErrors"/> list,  indicating that the trade date for the specified <paramref name="commandName"/>
    /// is invalid.</remarks>
    /// <param name="validationErrors">The list of validation errors to which any detected errors will be added.</param>
    /// <param name="tradeDate">The trade date to validate. Must not be <see cref="DateOnly.MinValue"/> or <see cref="DateOnly.MaxValue"/>.</param>
    /// <param name="commandName">The name of the command associated with the trade date, used in the validation error message.</param>
    /// <returns>The updated list of validation errors, including any new errors detected during validation.</returns>
    public static List<ValidationError> ValidateTradeDate(this List<ValidationError> validationErrors, DateOnly tradeDate, string commandName)
    {
        if (tradeDate == DateOnly.MinValue || tradeDate == DateOnly.MaxValue)
            validationErrors.Add(new ($"{commandName}.TradeDate is invalid"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified maturity date and adds an error to the validation errors list if the date is invalid.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to which any detected errors will be added.</param>
    /// <param name="maturityDate">The maturity date to validate. Must not be <see cref="DateOnly.MinValue"/> or <see cref="DateOnly.MaxValue"/>.</param>
    /// <param name="commandName">The name of the command associated with the validation, used in the error message if validation fails.</param>
    /// <returns>The updated list of validation errors, including any new errors detected during validation.</returns>
    public static List<ValidationError> ValidateMaturityDate(this List<ValidationError> validationErrors, DateOnly maturityDate, string commandName)
    {
        if (maturityDate == DateOnly.MinValue || maturityDate == DateOnly.MaxValue)
            validationErrors.Add(new ($"{commandName}.MaturityDate is invalid"));
        return validationErrors;
    }

   

    /// <summary>
    /// Validates the specified futures option contract ID and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>This method attempts to validate the provided futures option contract ID by creating an
    /// instance of <c>FuturesOptionContractId</c>. If the validation fails, an error message is added to the <paramref
    /// name="validationErrors"/> list.</remarks>
    /// <param name="validationErrors">The list to which validation errors will be added. Must not be null.</param>
    /// <param name="contractId">The futures option contract ID to validate. Cannot be null or empty.</param>
    /// <returns>The updated list of validation errors, including any errors encountered during the validation of the contract
    /// ID.</returns>
    public static List<ValidationError> ValidateFuturesOptionContractId(this List<ValidationError> validationErrors, string contractId)
    {
        try
        {
            var futuresOptionContractId = new FuturesOptionContractId(contractId);
        }
        catch (Exception ex)
        {
            validationErrors.Add(new ValidationError(ex.Message));
        }
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified <paramref name="valueDate"/> and adds a validation error to the  <paramref
    /// name="validationErrors"/> list if the date is invalid.
    /// </summary>
    /// <remarks>A <paramref name="valueDate"/> is considered invalid if it is equal to <see
    /// cref="DateOnly.MinValue"/>  or <see cref="DateOnly.MaxValue"/>. In such cases, a validation error message is
    /// added to the  <paramref name="validationErrors"/> list, indicating the invalidity of the value.</remarks>
    /// <param name="validationErrors">The list of validation errors to which any detected errors will be added.</param>
    /// <param name="valueDate">The date to validate. Must not be <see cref="DateOnly.MinValue"/> or <see cref="DateOnly.MaxValue"/>.</param>
    /// <param name="commandName">The name of the command associated with the validation, used in the error message.</param>
    /// <returns>The updated list of validation errors, including any new errors detected during validation.</returns>
    public static List<ValidationError> ValidateValueDate(this List<ValidationError> validationErrors, DateOnly valueDate, string commandName)
    {
        if (valueDate == DateOnly.MinValue || valueDate == DateOnly.MaxValue)
            validationErrors.Add(new ($"{commandName}.ValueDate is invalid"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified <paramref name="orderId"/> and adds a validation error to the  <paramref
    /// name="validationErrors"/> list if the value is invalid.
    /// </summary>
    /// <param name="validationErrors">The list to which validation errors will be added. This list must not be null.</param>
    /// <param name="orderId">The order ID to validate. Must be greater than 0.</param>
    /// <param name="commandName">The name of the command associated with the validation. Used to format the error message.</param>
    /// <returns>The updated list of validation errors, including any new errors added during validation.</returns>
    public static List<ValidationError> ValidateOrderId(this List<ValidationError> validationErrors, int orderId, string commandName)
    {
        if (orderId < 1)
            validationErrors.Add(new ($"{commandName}.OrderId must be > 0"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified trade ID and adds a validation error to the provided list if the trade ID is invalid.
    /// </summary>
    /// <remarks>This method checks whether the <paramref name="tradeId"/> is greater than 0. If the value is
    /// invalid, a validation error is added to the <paramref name="validationErrors"/> list with a message formatted
    /// using the <paramref name="commandName"/>.</remarks>
    /// <param name="validationErrors">The list of validation errors to which any detected errors will be added.</param>
    /// <param name="tradeId">The trade ID to validate. Must be greater than 0.</param>
    /// <param name="commandName">The name of the command associated with the validation, used to format the error message.</param>
    /// <returns>The updated list of validation errors, including any new errors detected during validation.</returns>
    public static List<ValidationError> ValidateTradeId(this List<ValidationError> validationErrors, int tradeId, string commandName)
    {
        if (tradeId <= 0)
            validationErrors.Add(new ValidationError($"{commandName}.TradeId must be > 0"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified number of days to expiry and adds a validation error if the value is invalid.
    /// </summary>
    /// <remarks>If <paramref name="daysToExpiry"/> is less than zero, a validation error is added to the 
    /// <paramref name="validationErrors"/> list with a message indicating the invalid value.</remarks>
    /// <param name="validationErrors">The list of validation errors to which any new errors will be added.</param>
    /// <param name="daysToExpiry">The number of days to expiry. Must be zero or greater.</param>
    /// <param name="commandName">The name of the command being validated, used to construct the error message.</param>
    /// <returns>The updated list of validation errors, including any new errors added during validation.</returns>
    public static List<ValidationError> ValidateDaysToExpiry(this List<ValidationError> validationErrors, int daysToExpiry, string commandName)
    {
        if (daysToExpiry < 0)
            validationErrors.Add(new ($"{commandName}.DaysToExpiry is invalid"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified import date and adds a validation error if the date is invalid.
    /// </summary>
    /// <remarks>An import date is considered invalid if it is equal to <see cref="DateTime.MinValue"/> or
    /// <see cref="DateTime.MaxValue"/>.  In such cases, a validation error is added to the provided <paramref
    /// name="validationErrors"/> list.</remarks>
    /// <param name="validationErrors">The list of validation errors to which any new errors will be added.</param>
    /// <param name="importDate">The import date to validate. Must not be <see cref="DateTime.MinValue"/> or <see cref="DateTime.MaxValue"/>.</param>
    /// <param name="commandName">The name of the command associated with the import date, used in the validation error message.</param>
    /// <returns>The updated list of validation errors, including any new errors related to the import date.</returns>
    public static List<ValidationError> ValidateImportDate(this List<ValidationError> validationErrors, DateTime importDate, string commandName)
    {
        if (importDate == DateTime.MinValue || importDate == DateTime.MaxValue)
            validationErrors.Add(new ($"{commandName}.ImportDate is invalid"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the properties of an <see cref="OptionTradeEntityId"/> and adds any validation errors to the provided
    /// list.
    /// </summary>
    /// <remarks>This method checks whether the <see cref="OptionTradeEntityId.OrderId"/> and <see
    /// cref="OptionTradeEntityId.TradeId"/>  properties are greater than 0. If either property is invalid, a
    /// corresponding error message is added to the  <paramref name="validationErrors"/> list.</remarks>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="optionTradeId">The <see cref="OptionTradeEntityId"/> instance to validate. Its <see cref="OptionTradeEntityId.OrderId"/> and 
    /// <see cref="OptionTradeEntityId.TradeId"/> properties must be greater than 0.</param>
    /// <param name="commandName">The name of the command being validated, used to format error messages.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any new validation errors found.</returns>
    public static List<ValidationError> ValidateOptionTradeId(this List<ValidationError> validationErrors, OptionTradeEntityId optionTradeId, string commandName)
    {
        if (optionTradeId.OrderId < 1)
            validationErrors.Add(new ($"{commandName}.OrderId must be > 0"));
        if (optionTradeId.TradeId < 1)
            validationErrors.Add(new ($"{commandName}.TradeId must be > 0"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the <see cref="TradePlanEntityId"/> object and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>This method checks whether the <see cref="TradePlanEntityId.OrderId"/> and <see
    /// cref="TradePlanEntityId.TradeId"/> properties of the <paramref name="tradePlanId"/> are greater than 0. If
    /// either property is invalid, a corresponding validation error message is added to the <paramref
    /// name="validationErrors"/> list.</remarks>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="tradePlanId">The <see cref="TradePlanEntityId"/> object to validate. Its <see cref="TradePlanEntityId.OrderId"/> and <see
    /// cref="TradePlanEntityId.TradeId"/> properties must be greater than 0.</param>
    /// <param name="commandName">The name of the command being validated, used to provide context in the validation error messages.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any new validation errors found.</returns>
    public static List<ValidationError> ValidateTradePlanId(this List<ValidationError> validationErrors, TradePlanEntityId tradePlanId, string commandName)
    {
        if (tradePlanId.OrderId < 1)
            validationErrors.Add(new ($"{commandName}.OrderId must be > 0"));
        if (tradePlanId.TradeId < 1)
            validationErrors.Add(new ($"{commandName}.TradeId must be > 0"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the properties of a <see cref="TradePlanForwardLossLimitEntityId"/> instance and adds any validation
    /// errors to the provided list.
    /// </summary>
    /// <remarks>This method checks the following conditions: <list type="bullet">
    /// <item><description><paramref name="id"/>.<see cref="TradePlanForwardLossLimitEntityId.OrderId"/> must be greater
    /// than 0.</description></item> <item><description><paramref name="id"/>.<see
    /// cref="TradePlanForwardLossLimitEntityId.TradeId"/> must be greater than 0.</description></item>
    /// <item><description><paramref name="id"/>.<see cref="TradePlanForwardLossLimitEntityId.TradeType"/> must not be
    /// <see cref="TradeType.Unknown"/>.</description></item> <item><description><paramref name="id"/>.<see
    /// cref="TradePlanForwardLossLimitEntityId.ValueDate"/> must be a valid date (not <see cref="DateOnly.MinValue"/>
    /// or <see cref="DateOnly.MaxValue"/>).</description></item> </list> If any of these conditions are not met, a
    /// corresponding <see cref="ValidationError"/> is added to the <paramref name="validationErrors"/> list.</remarks>
    /// <param name="validationErrors">The list of <see cref="ValidationError"/> objects to which validation errors will be added.</param>
    /// <param name="id">The <see cref="TradePlanForwardLossLimitEntityId"/> instance to validate.</param>
    /// <param name="commandName">The name of the command being validated, used to prefix error messages.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any new validation errors found during the
    /// validation process.</returns>
    public static List<ValidationError> ValidateTradePlanForwardLossLimitId(this List<ValidationError> validationErrors, TradePlanForwardLossLimitEntityId id, string commandName)
    {
        if (id.OrderId < 1)
            validationErrors.Add(new ($"{commandName}.OrderId must be > 0"));
        if (id.TradeId < 1)
            validationErrors.Add(new ($"{commandName}.TradeId must be > 0"));
        if (id.TradeType == TradeType.Unknown)
            validationErrors.Add(new ($"{commandName}.TradeType must not be Unknown"));
        if (id.ValueDate == DateOnly.MinValue || id.ValueDate == DateOnly.MaxValue)
            validationErrors.Add(new ($"{commandName}.ValueDate must be valid"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified <paramref name="baseContractId"/> and adds a validation error to the <paramref
    /// name="validationErrors"/> list if it is null, empty, or consists only of whitespace.
    /// </summary>
    /// <param name="validationErrors">The list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="baseContractId">The base contract identifier to validate. Must not be null, empty, or whitespace.</param>
    /// <param name="commandName">The name of the command associated with the validation. Used to construct the validation error message.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any new validation errors.</returns>
    public static List<ValidationError> ValidateBaseContractId(this List<ValidationError> validationErrors, string baseContractId, string commandName)
    {
        if (string.IsNullOrWhiteSpace(baseContractId))
            validationErrors.Add(new ($"{commandName}.BaseContractId is empty"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified futures option contracts identifier and adds a validation error if it is null, empty, or
    /// consists only of whitespace.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to which any new validation errors will be added.</param>
    /// <param name="futuresOptionContractsId">The identifier of the futures option contracts to validate. Cannot be null, empty, or whitespace.</param>
    /// <param name="commandName">The name of the command associated with the validation, used in the error message if validation fails.</param>
    /// <returns>The updated list of validation errors, including any new errors added during validation.</returns>
    public static List<ValidationError> ValidateFuturesOptionContractsId(this List<ValidationError> validationErrors, string futuresOptionContractsId, string commandName)
    {
        if (string.IsNullOrWhiteSpace(futuresOptionContractsId))
            validationErrors.Add(new ($"{commandName}.ContractsId is empty"));
        return validationErrors;
    }
}
