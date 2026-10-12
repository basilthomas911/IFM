using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.Command.Validation;

/// <summary>Pure ordered aggregate validation for portfolio mutation commands.</summary>
public static class PortfolioCommandValidation
{
    /// <summary>Validates the CreatePortfolioCommand payload and owning identity.</summary>
    public static TErrors ValidatePortfolioCommand<TErrors>(this TErrors errors, CreatePortfolioCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateCreate(errors, typed);
        return errors;
    }
    /// <summary>Validates the AddPortfolioVersionCommand payload and owning identity.</summary>
    public static TErrors ValidatePortfolioCommand<TErrors>(this TErrors errors, AddPortfolioVersionCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateVersion(errors, typed);
        return errors;
    }
    /// <summary>Validates the ChangePortfolioOperatingStateCommand payload and owning identity.</summary>
    public static TErrors ValidatePortfolioCommand<TErrors>(this TErrors errors, ChangePortfolioOperatingStateCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateStateChange(errors, typed);
        return errors;
    }
    /// <summary>Validates the AddFundToPortfolioCommand payload and owning identity.</summary>
    public static TErrors ValidatePortfolioCommand<TErrors>(this TErrors errors, AddFundToPortfolioCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateFund(errors, typed);
        return errors;
    }
    /// <summary>Validates the DelegateFundAllocationCommand payload and owning identity.</summary>
    public static TErrors ValidatePortfolioCommand<TErrors>(this TErrors errors, DelegateFundAllocationCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateAllocation(errors, typed);
        return errors;
    }
    /// <summary>Validates the DelegateFundRiskEnvelopeCommand payload and owning identity.</summary>
    public static TErrors ValidatePortfolioCommand<TErrors>(this TErrors errors, DelegateFundRiskEnvelopeCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateRiskEnvelope(errors, typed);
        return errors;
    }
    /// <summary>Validates the RetirePortfolioCommand payload and owning identity.</summary>
    public static TErrors ValidatePortfolioCommand<TErrors>(this TErrors errors, RetirePortfolioCommand typed)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(typed);
        
        ValidateIdentity(errors, typed);
        ValidateRetire(errors, typed);
        return errors;
    }
    /// <summary>Validates the DeleteDraftPortfolioCommand payload and owning identity.</summary>
    public static TErrors ValidatePortfolioCommand<TErrors>(this TErrors errors, DeleteDraftPortfolioCommand typed)

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
        ICollection<ValidationError> errors,        ICommand<PortfolioId> command)
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
    static void ValidateCreate(ICollection<ValidationError> errors,CreatePortfolioCommand command)
    {
        if (command.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.IdempotencyKey is empty"));
        if (command.Portfolio is null)
        {
            errors.Add(new($"{command.CommandName}.Portfolio is null"));
            return;
        }
        if (command.Portfolio.BrokerAccountRefs is null)
            errors.Add(new($"{command.CommandName}.Portfolio.BrokerAccountRefs is null"));
        else
            AddErrors(errors, command.Portfolio.Validate(requireActivePolicy: false), command.CommandName);
        if (command.Portfolio.PortfolioId != command.EntityId?.Id)
            errors.Add(new($"{command.CommandName}.Portfolio.PortfolioId does not match EntityId"));
    }

    /// <summary>Checks ValidateVersion without changing state.</summary>
    static void ValidateVersion(ICollection<ValidationError> errors,AddPortfolioVersionCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        if (command.Portfolio is null)
        {
            errors.Add(new($"{command.CommandName}.Portfolio is null"));
            return;
        }
        if (command.Portfolio.BrokerAccountRefs is null)
            errors.Add(new($"{command.CommandName}.Portfolio.BrokerAccountRefs is null"));
        else
            AddErrors(errors, command.Portfolio.Validate(), command.CommandName);
        if (command.Portfolio.PortfolioId != command.EntityId?.Id)
            errors.Add(new($"{command.CommandName}.Portfolio.PortfolioId does not match EntityId"));
    }

    /// <summary>Checks ValidateStateChange without changing state.</summary>
    static void ValidateStateChange(ICollection<ValidationError> errors,ChangePortfolioOperatingStateCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        if (command.State == PortfolioOperatingState.Unknown)
            errors.Add(new($"{command.CommandName}.State is required"));
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    /// <summary>Checks ValidateFund without changing state.</summary>
    static void ValidateFund(ICollection<ValidationError> errors,AddFundToPortfolioCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedPortfolioVersion, command.CommandName);
        if (command.FundId is null)
            errors.Add(new($"{command.CommandName}.FundId is null"));
        else
        {
            AddErrors(errors, command.FundId.Validate(), command.CommandName);
            if (command.FundId.PortfolioId != command.EntityId?.Id)
                errors.Add(new($"{command.CommandName}.FundId.PortfolioId does not match EntityId"));
        }
    }

    /// <summary>Checks ValidateAllocation without changing state.</summary>
    static void ValidateAllocation(ICollection<ValidationError> errors,DelegateFundAllocationCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedPortfolioVersion, command.CommandName);
        if (command.Allocation is null)
            errors.Add(new($"{command.CommandName}.Allocation is null"));
        else
        {
            AddErrors(errors, command.Allocation.Validate(), command.CommandName);
            if (command.Allocation.PortfolioId != command.EntityId?.Id)
                errors.Add(new($"{command.CommandName}.Allocation.PortfolioId does not match EntityId"));
        }
    }

    /// <summary>Checks ValidateRiskEnvelope without changing state.</summary>
    static void ValidateRiskEnvelope(ICollection<ValidationError> errors,DelegateFundRiskEnvelopeCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedPortfolioVersion, command.CommandName);
        if (command.Envelope is null)
            errors.Add(new($"{command.CommandName}.Envelope is null"));
        else
        {
            AddErrors(errors, command.Envelope.Validate(), command.CommandName);
            if (command.Envelope.PortfolioId != command.EntityId?.Id)
                errors.Add(new($"{command.CommandName}.Envelope.PortfolioId does not match EntityId"));
        }
    }

    /// <summary>Checks ValidateRetire without changing state.</summary>
    static void ValidateRetire(ICollection<ValidationError> errors,RetirePortfolioCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    /// <summary>Checks ValidateDelete without changing state.</summary>
    static void ValidateDelete(ICollection<ValidationError> errors,DeleteDraftPortfolioCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    /// <summary>Checks ValidateExpectedVersion without changing state.</summary>
    static void ValidateExpectedVersion(ICollection<ValidationError> errors,long expectedVersion,string commandName)
    {
        if (expectedVersion < 0)
            errors.Add(new($"{commandName}.ExpectedVersion cannot be negative"));
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
