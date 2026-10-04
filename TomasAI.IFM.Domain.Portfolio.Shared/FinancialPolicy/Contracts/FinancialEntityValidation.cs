using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Intrinsic validation for the financial actor identities.</summary>
public static class FinancialEntityValidation
{
    /// <summary>Validates the portfolio and operation identity.</summary>
    public static List<ValidationError> ValidateFinancialEntityId(this List<ValidationError> errors, FinancialExecutionId? id, string commandName)
        => Append(errors, id?.Validate(), commandName);

    /// <summary>Validates the portfolio ledger identity.</summary>
    public static List<ValidationError> ValidateFinancialEntityId(this List<ValidationError> errors, LedgerPortfolioId? id, string commandName)
        => Append(errors, id?.Validate(), commandName);

    /// <summary>Validates the portfolio and capacity reservation identity.</summary>
    public static List<ValidationError> ValidateFinancialEntityId(this List<ValidationError> errors, CapacityReservationEntityId? id, string commandName)
        => Append(errors, id?.Validate(), commandName);

    /// <summary>Appends all identity errors, including a missing identifier.</summary>
    private static List<ValidationError> Append(List<ValidationError> errors, IReadOnlyList<string>? failures, string commandName)
    {
        if (failures is null) errors.Add(new($"{commandName}.EntityId is required."));
        else errors.AddRange(failures.Select(failure => new ValidationError($"{commandName}.EntityId: {failure}")));
        return errors;
    }
}
