using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Intrinsic validation for the financial actor identities.</summary>
public static class FinancialEntityValidation
{

    extension(List<ValidationError> errors)
    {
        /// <summary>Validates the portfolio and operation identity.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete financial model collection contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete financial model collection contract.")]
        public List<ValidationError> ValidateFinancialEntityId(FinancialExecutionId? id, string commandName)

        {
            ArgumentNullException.ThrowIfNull(errors);
            return Append(errors, id?.Validate(), commandName);
        }

        /// <summary>Validates the portfolio ledger identity.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete financial model collection contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete financial model collection contract.")]
        public List<ValidationError> ValidateFinancialEntityId(LedgerPortfolioId? id, string commandName)

        {
            ArgumentNullException.ThrowIfNull(errors);
            return Append(errors, id?.Validate(), commandName);
        }

        /// <summary>Validates the portfolio and capacity reservation identity.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete financial model collection contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete financial model collection contract.")]
        public List<ValidationError> ValidateFinancialEntityId(CapacityReservationEntityId? id, string commandName)

        {
            ArgumentNullException.ThrowIfNull(errors);
            return Append(errors, id?.Validate(), commandName);
        }
    }

    /// <summary>Appends all identity errors, including a missing identifier.</summary>
    private static List<ValidationError> Append(List<ValidationError> errors, IReadOnlyList<string>? failures, string commandName)
    {
        if (failures is null) errors.Add(new($"{commandName}.EntityId is required."));
        else errors.AddRange(failures.Select(failure => new ValidationError($"{commandName}.EntityId: {failure}")));
        return errors;
    }
}
