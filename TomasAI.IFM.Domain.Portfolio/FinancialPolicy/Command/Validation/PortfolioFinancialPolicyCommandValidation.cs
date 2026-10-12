using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Validation;

/// <summary>Pure ordered aggregate validation for portfolio mutation commands.</summary>
public static class PortfolioFinancialPolicyCommandValidation
{
    /// <summary>Validates the CreatePortfolioFinancialPolicyCommand payload and owning identity.</summary>
    public static TErrors ValidateFinancialPolicyCommand<TErrors>(this TErrors errors, CreatePortfolioFinancialPolicyCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateCreate(errors, typed);
        return errors;
    }
    /// <summary>Validates the AddPortfolioFinancialPolicyVersionCommand payload and owning identity.</summary>
    public static TErrors ValidateFinancialPolicyCommand<TErrors>(this TErrors errors, AddPortfolioFinancialPolicyVersionCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateVersion(errors, typed);
        return errors;
    }
    /// <summary>Validates the ActivateAndAssignPortfolioFinancialPolicyCommand payload and owning identity.</summary>
    public static TErrors ValidateFinancialPolicyCommand<TErrors>(this TErrors errors, ActivateAndAssignPortfolioFinancialPolicyCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateActivation(errors, typed);
        return errors;
    }
    /// <summary>Validates the RetirePortfolioFinancialPolicyCommand payload and owning identity.</summary>
    public static TErrors ValidateFinancialPolicyCommand<TErrors>(this TErrors errors, RetirePortfolioFinancialPolicyCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateRetire(errors, typed);
        return errors;
    }
    /// <summary>Validates the DeleteDraftPortfolioFinancialPolicyCommand payload and owning identity.</summary>
    public static TErrors ValidateFinancialPolicyCommand<TErrors>(this TErrors errors, DeleteDraftPortfolioFinancialPolicyCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateDelete(errors, typed);
        return errors;
    }
    /// <summary>Checks ValidateIdentity without changing state.</summary>
    static void ValidateIdentity(
        ICollection<ValidationError> errors,        ICommand<PortfolioFinancialPolicyId> command)
    {
        if (command.EntityId is null)
        {
            return;
        }
        AddErrors(errors, command.EntityId.Validate(), command.CommandName);
        if (!string.Equals(command.Subject.EntityId, command.EntityId.Format(), StringComparison.Ordinal))
            errors.Add(new($"{command.CommandName}.EntityId does not match Subject.EntityId"));
    }

    /// <summary>Checks ValidateCreate without changing state.</summary>
    static void ValidateCreate(ICollection<ValidationError> errors,CreatePortfolioFinancialPolicyCommand command)
    {
        if (command.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.IdempotencyKey is empty"));
        ValidatePolicy(errors, command, command.Policy);
    }

    /// <summary>Checks ValidateVersion without changing state.</summary>
    static void ValidateVersion(ICollection<ValidationError> errors,AddPortfolioFinancialPolicyVersionCommand command)
    {
        ValidateExpectedRevision(errors, command.ExpectedVersion, command.CommandName);
        ValidatePolicy(errors, command, command.Policy);
    }

    /// <summary>Checks ValidateActivation without changing state.</summary>
    static void ValidateActivation(ICollection<ValidationError> errors,ActivateAndAssignPortfolioFinancialPolicyCommand command)
    {
        if (command.PolicyVersion <= 0)
            errors.Add(new($"{command.CommandName}.PolicyVersion must be positive"));
        ValidateExpectedRevision(errors, command.ExpectedPolicyRevision, command.CommandName);
        ValidateExpectedRevision(errors, command.ExpectedPortfolioRevision, command.CommandName);
    }

    /// <summary>Checks ValidateRetire without changing state.</summary>
    static void ValidateRetire(ICollection<ValidationError> errors,RetirePortfolioFinancialPolicyCommand command)
    {
        if (command.PolicyVersion <= 0)
            errors.Add(new($"{command.CommandName}.PolicyVersion must be positive"));
        ValidateExpectedRevision(errors, command.ExpectedRevision, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    /// <summary>Checks ValidateDelete without changing state.</summary>
    static void ValidateDelete(ICollection<ValidationError> errors,DeleteDraftPortfolioFinancialPolicyCommand command)
    {
        ValidateExpectedRevision(errors, command.ExpectedRevision, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    /// <summary>Checks ValidatePolicy without changing state.</summary>
    static void ValidatePolicy(
        ICollection<ValidationError> errors,        ICommand<PortfolioFinancialPolicyId> command,        PortfolioFinancialPolicyReadModel? policy)
    {
        if (policy is null)
        {
            errors.Add(new($"{command.CommandName}.Policy is null"));
            return;
        }
        if (policy.TradeFamilyLimits is null || policy.TradeFamilyLimits.Any(static family => family is null))
            errors.Add(new($"{command.CommandName}.Policy.TradeFamilyLimits contains null values"));
        else
            AddErrors(errors, policy.Validate(), command.CommandName);
        if (command.EntityId is null)
            return;
        if (policy.PortfolioId != command.EntityId?.PortfolioId || policy.PolicyId != command.EntityId.PolicyId)
            errors.Add(new($"{command.CommandName}.Policy identity does not match EntityId"));
    }

    /// <summary>Checks ValidateExpectedRevision without changing state.</summary>
    static void ValidateExpectedRevision(ICollection<ValidationError> errors,long expectedRevision,string commandName)
    {
        if (expectedRevision < 0)
            errors.Add(new($"{commandName}.expected revision cannot be negative"));
    }

    /// <summary>Checks ValidateReason without changing state.</summary>
    static void ValidateReason(ICollection<ValidationError> errors,string? reason,string commandName)
    {
        if (string.IsNullOrWhiteSpace(reason))
            errors.Add(new($"{commandName}.Reason is required"));
    }

    /// <summary>Checks AddErrors without changing state.</summary>
    static void AddErrors(ICollection<ValidationError> errors,IEnumerable<string> messages,string commandName)
    {
        foreach (var message in messages)
            errors.Add(new($"{commandName}.{message}"));
    }
}
