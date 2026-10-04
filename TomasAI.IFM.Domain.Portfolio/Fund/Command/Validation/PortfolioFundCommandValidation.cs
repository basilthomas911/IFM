using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Command;
using TomasAI.IFM.Domain.Portfolio.Fund.Command;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;



namespace TomasAI.IFM.Domain.Portfolio.Fund.Command.Validation;

/// <summary>Accumulates Fund command payload and identity errors before state loading.</summary>
public static class PortfolioFundCommandValidation
{
    /// <summary>Validates the SynchronizeFundRiskOutcomeCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, SynchronizeFundRiskOutcomeCommand typed)
    {
        ValidateIdentity(errors, typed);
        if (typed.Evidence is not { } e || typed.ExpectedVersion <= 0 || e.PortfolioId != typed.EntityId?.PortfolioId || e.FundId != typed.EntityId?.FundId)
            errors.Add(new("Exact terminal evidence and Fund version are required."));
        return errors;
    }
    /// <summary>Validates the AuthorizeFundOrderRiskCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, AuthorizeFundOrderRiskCommand typed)
    {
        errors.ValidateFundRiskAuthorization(typed);
        
        ValidateIdentity(errors, typed);
        return errors;
    }
    /// <summary>Validates the CreateFundMandateCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, CreateFundMandateCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateCreate(errors, typed);
        return errors;
    }
    /// <summary>Validates the AddFundMandateVersionCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, AddFundMandateVersionCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateVersion(errors, typed);
        return errors;
    }
    /// <summary>Validates the ChangeFundOperatingStateCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, ChangeFundOperatingStateCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateStateChange(errors, typed);
        return errors;
    }
    /// <summary>Validates the AssignTradeTemplateCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, AssignTradeTemplateCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateAssignment(errors, typed);
        return errors;
    }
    /// <summary>Validates the ReserveFundOrderCompositionCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, ReserveFundOrderCompositionCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateReservation(errors, typed);
        return errors;
    }
    /// <summary>Validates the CreateManualFundOrderCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, CreateManualFundOrderCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateManualOrder(errors, typed);
        return errors;
    }
    /// <summary>Validates the AddManualFundOrderTradeCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, AddManualFundOrderTradeCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateManualTrade(errors, typed);
        return errors;
    }
    /// <summary>Validates the RemoveManualFundOrderTradeCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, RemoveManualFundOrderTradeCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateManualTradeMutation(errors, typed.Request, typed.EntityId, typed.CommandName, false);
        return errors;
    }
    /// <summary>Validates the ChangeManualFundOrderTradeStateCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, ChangeManualFundOrderTradeStateCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateManualTradeMutation(errors, typed.Request, typed.EntityId, typed.CommandName, true);
        return errors;
    }
    /// <summary>Validates the CloseManualFundOrderCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, CloseManualFundOrderCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateManualOrderMutation(errors, typed.Request, typed.EntityId, typed.CommandName);
        return errors;
    }
    /// <summary>Validates the DeleteManualFundOrderCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, DeleteManualFundOrderCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateManualOrderMutation(errors, typed.Request, typed.EntityId, typed.CommandName);
        return errors;
    }
    /// <summary>Validates the MarkFundOrderComposingCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, MarkFundOrderComposingCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateMarkComposing(errors, typed);
        return errors;
    }
    /// <summary>Validates the RecordFundOrderComposedCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, RecordFundOrderComposedCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateCompositionResult(errors, typed);
        return errors;
    }
    /// <summary>Validates the RecordFundOrderRiskOutcomeCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, RecordFundOrderRiskOutcomeCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateRiskResult(errors, typed);
        return errors;
    }
    /// <summary>Validates the CancelFundOrderCompositionCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, CancelFundOrderCompositionCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateCancel(errors, typed);
        return errors;
    }
    /// <summary>Validates the ExpireFundOrderCompositionCommand payload and its Fund identity.</summary>
    public static List<ValidationError> ValidateFundCommand(this List<ValidationError> errors, ExpireFundOrderCompositionCommand typed)
    {
        ValidateIdentity(errors, typed);
        ValidateExpire(errors, typed);
        return errors;
    }
    /// <summary>Checks ValidateIdentity without changing state.</summary>
    static void ValidateIdentity(
        List<ValidationError> errors,
        ICommand<PortfolioFundId> command)
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
    static void ValidateCreate(List<ValidationError> errors, CreateFundMandateCommand command)
    {
        if (command.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.IdempotencyKey is empty"));
        ValidateMandate(errors, command, command.Mandate);
    }

    /// <summary>Checks ValidateVersion without changing state.</summary>
    static void ValidateVersion(List<ValidationError> errors, AddFundMandateVersionCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        ValidateMandate(errors, command, command.Mandate);
    }

    /// <summary>Checks ValidateStateChange without changing state.</summary>
    static void ValidateStateChange(List<ValidationError> errors, ChangeFundOperatingStateCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        if (command.State == FundOperatingState.Unknown)
            errors.Add(new($"{command.CommandName}.State is required"));
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    /// <summary>Checks ValidateAssignment without changing state.</summary>
    static void ValidateAssignment(List<ValidationError> errors, AssignTradeTemplateCommand command)
    {
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        if (command.Assignment is null)
        {
            errors.Add(new($"{command.CommandName}.Assignment is null"));
            return;
        }
        if (command.Assignment.UnderlyingUniverse is null)
            errors.Add(new($"{command.CommandName}.Assignment.UnderlyingUniverse is null"));
        else
            AddErrors(errors, command.Assignment.Validate(), command.CommandName);
        if (command.Assignment.PortfolioId != command.EntityId?.PortfolioId ||
            command.Assignment.FundId != command.EntityId?.FundId)
            errors.Add(new($"{command.CommandName}.Assignment identity does not match EntityId"));
    }

    /// <summary>Checks ValidateReservation without changing state.</summary>
    static void ValidateReservation(List<ValidationError> errors, ReserveFundOrderCompositionCommand command)
    {
        var request = command.Request;
        var snapshot = command.Snapshot;
        if (request is null)
        {
            errors.Add(new($"{command.CommandName}.Request is null"));
            return;
        }
        if (snapshot is null)
        {
            errors.Add(new($"{command.CommandName}.Snapshot is null"));
            return;
        }
        if (request.PortfolioId != command.EntityId?.PortfolioId || request.FundId != command.EntityId?.FundId)
            errors.Add(new($"{command.CommandName}.Request identity does not match EntityId"));
        if (request.WorkflowId == Guid.Empty || request.WorkflowRevision <= 0 ||
            request.TradeSelectionInvocationId == Guid.Empty || request.TradeSelectionResultId == Guid.Empty)
            errors.Add(new($"{command.CommandName}.Request workflow identity is invalid"));
        if (request.PortfolioVersion <= 0 || request.FundMandateVersion <= 0 ||
            request.TradeTemplateId == Guid.Empty || request.TradeTemplateVersion <= 0 ||
            request.OrderCompositionProfileId == Guid.Empty || request.OrderCompositionProfileVersion <= 0)
            errors.Add(new($"{command.CommandName}.Request versioned identities are invalid"));
        if (request.IdempotencyKey == Guid.Empty || string.IsNullOrWhiteSpace(request.TradeSelectionResultSha256) ||
            string.IsNullOrWhiteSpace(request.PortfolioFundStrategySnapshotSha256))
            errors.Add(new($"{command.CommandName}.Request hashes and idempotency key are required"));
        if (string.IsNullOrWhiteSpace(request.UnderlyingRoot) || string.IsNullOrWhiteSpace(request.DecisionHorizon) ||
            request.TradeInstructions is null || request.TradeInstructions.Length == 0 ||
            request.TradeInstructions.Any(static instruction => instruction is null))
            errors.Add(new($"{command.CommandName}.Request trade instructions are required"));
        ValidateUtcWindow(errors, request.RequestedAtUtc, request.ExpiresAtUtc, command.CommandName);
        if (snapshot.Portfolio is null || snapshot.Fund is null || snapshot.Allocation is null ||
            snapshot.RiskEnvelope is null || snapshot.FinancialPolicy is null || snapshot.Assignments is null ||
            snapshot.Assignments.Any(static assignment => assignment is null))
            errors.Add(new($"{command.CommandName}.Snapshot contains null values"));
        else if (snapshot.WorkflowId != request.WorkflowId || snapshot.WorkflowRevision != request.WorkflowRevision ||
                 snapshot.Portfolio.PortfolioId != request.PortfolioId || snapshot.Fund.PortfolioId != request.PortfolioId ||
                 snapshot.Fund.FundId != request.FundId)
            errors.Add(new($"{command.CommandName}.Snapshot does not match Request"));
        if (snapshot.ResolvedAtUtc.Kind != DateTimeKind.Utc || snapshot.ValidUntilUtc.Kind != DateTimeKind.Utc ||
            snapshot.ValidUntilUtc <= snapshot.ResolvedAtUtc || string.IsNullOrWhiteSpace(snapshot.PayloadSha256))
            errors.Add(new($"{command.CommandName}.Snapshot validity is invalid"));
    }

    /// <summary>Checks ValidateManualOrder without changing state.</summary>
    static void ValidateManualOrder(List<ValidationError> errors, CreateManualFundOrderCommand command)
    {
        var request = command.Request;
        if (request is null)
        {
            errors.Add(new($"{command.CommandName}.Request is null"));
            return;
        }
        if (request.PortfolioId != command.EntityId?.PortfolioId || request.FundId != command.EntityId?.FundId)
            errors.Add(new($"{command.CommandName}.Request identity does not match EntityId"));
        if (request.PortfolioVersion <= 0 || request.FundMandateVersion <= 0 || request.IdempotencyKey == Guid.Empty)
            errors.Add(new($"{command.CommandName}.Request version and idempotency values are invalid"));
        ValidateUtcWindow(errors, request.RequestedAtUtc, request.ExpiresAtUtc, command.CommandName);
    }

    /// <summary>Checks ValidateManualTrade without changing state.</summary>
    static void ValidateManualTrade(List<ValidationError> errors, AddManualFundOrderTradeCommand command)
    {
        var request = command.Request;
        if (request is null)
        {
            errors.Add(new($"{command.CommandName}.Request is null"));
            return;
        }
        if (request.PortfolioId != command.EntityId?.PortfolioId || request.FundId != command.EntityId?.FundId)
            errors.Add(new($"{command.CommandName}.Request identity does not match EntityId"));
        if (request.OrderId <= 0 || request.ExpectedOrderVersion <= 0 || request.TradeId <= 0)
            errors.Add(new($"{command.CommandName}.Request order and trade identities are invalid"));
        if (string.IsNullOrWhiteSpace(request.TradeType) ||
            string.IsNullOrWhiteSpace(request.TradeState) ||
            string.IsNullOrWhiteSpace(request.TradeAction) ||
            string.IsNullOrWhiteSpace(request.Reference) ||
            string.IsNullOrWhiteSpace(request.BaseContractSymbol))
            errors.Add(new($"{command.CommandName}.Request trade fields are required"));
        if (request.RequestedAtUtc.Kind != DateTimeKind.Utc)
            errors.Add(new($"{command.CommandName}.Request.RequestedAtUtc must be UTC"));
    }

    /// <summary>Checks ValidateManualTradeMutation without changing state.</summary>
    static void ValidateManualTradeMutation(
        List<ValidationError> errors,
        ManualFundOrderTradeMutationRequest? request,
        PortfolioFundId? entityId,
        string commandName,
        bool requireState)
    {
        if (request is null)
        {
            errors.Add(new($"{commandName}.Request is null"));
            return;
        }
        if (entityId is not null &&
            (request.PortfolioId != entityId?.PortfolioId || request.FundId != entityId?.FundId))
            errors.Add(new($"{commandName}.Request identity does not match EntityId"));
        if (request.OrderId <= 0 || request.ExpectedOrderVersion <= 0 || request.TradeId <= 0)
            errors.Add(new($"{commandName}.Request order and trade identities are invalid"));
        if (requireState && string.IsNullOrWhiteSpace(request.TradeState))
            errors.Add(new($"{commandName}.Request.TradeState is required"));
        if (request.RequestedAtUtc.Kind != DateTimeKind.Utc)
            errors.Add(new($"{commandName}.Request.RequestedAtUtc must be UTC"));
    }

    /// <summary>Checks ValidateManualOrderMutation without changing state.</summary>
    static void ValidateManualOrderMutation(
        List<ValidationError> errors,
        ManualFundOrderMutationRequest? request,
        PortfolioFundId? entityId,
        string commandName)
    {
        if (request is null)
        {
            errors.Add(new($"{commandName}.Request is null"));
            return;
        }
        if (entityId is not null &&
            (request.PortfolioId != entityId?.PortfolioId || request.FundId != entityId?.FundId))
            errors.Add(new($"{commandName}.Request identity does not match EntityId"));
        if (request.OrderId <= 0 || request.ExpectedOrderVersion <= 0)
            errors.Add(new($"{commandName}.Request order identity is invalid"));
        if (request.RequestedAtUtc.Kind != DateTimeKind.Utc)
            errors.Add(new($"{commandName}.Request.RequestedAtUtc must be UTC"));
    }

    /// <summary>Checks ValidateMarkComposing without changing state.</summary>
    static void ValidateMarkComposing(List<ValidationError> errors, MarkFundOrderComposingCommand command)
    {
        ValidateOrderId(errors, command.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        if (command.InvocationId == Guid.Empty)
            errors.Add(new($"{command.CommandName}.InvocationId is empty"));
    }

    /// <summary>Checks ValidateCompositionResult without changing state.</summary>
    static void ValidateCompositionResult(List<ValidationError> errors, RecordFundOrderComposedCommand command)
    {
        ValidateOrderId(errors, command.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        var result = command.Result;
        if (result is null || result.ResultId == Guid.Empty || result.InvocationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(result.ResultSha256))
            errors.Add(new($"{command.CommandName}.Result identity is invalid"));
        else
            ValidateUtcWindow(errors, result.EvaluatedAtUtc, result.ExpiresAtUtc, command.CommandName);
    }

    /// <summary>Checks ValidateRiskResult without changing state.</summary>
    static void ValidateRiskResult(List<ValidationError> errors, RecordFundOrderRiskOutcomeCommand command)
    {
        ValidateOrderId(errors, command.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        var result = command.Result;
        if (result is null || result.ResultId == Guid.Empty || result.EnvelopeId == Guid.Empty ||
            result.EnvelopeVersion <= 0 || result.Decision == RiskDecision.Unknown ||
            string.IsNullOrWhiteSpace(result.ResultSha256) || string.IsNullOrWhiteSpace(result.CandidateSha256))
            errors.Add(new($"{command.CommandName}.Result identity is invalid"));
        else
            ValidateUtcWindow(errors, result.EvaluatedAtUtc, result.ExpiresAtUtc, command.CommandName);
    }

    /// <summary>Checks ValidateCancel without changing state.</summary>
    static void ValidateCancel(List<ValidationError> errors, CancelFundOrderCompositionCommand command)
    {
        ValidateOrderId(errors, command.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    /// <summary>Checks ValidateExpire without changing state.</summary>
    static void ValidateExpire(List<ValidationError> errors, ExpireFundOrderCompositionCommand command)
    {
        ValidateOrderId(errors, command.OrderId, command.EntityId, command.CommandName);
        ValidateExpectedVersion(errors, command.ExpectedVersion, command.CommandName);
        ValidateReason(errors, command.Reason, command.CommandName);
    }

    /// <summary>Checks ValidateMandate without changing state.</summary>
    static void ValidateMandate(
        List<ValidationError> errors,
        ICommand<PortfolioFundId> command,
        FundMandateReadModel? mandate)
    {
        if (mandate is null)
        {
            errors.Add(new($"{command.CommandName}.Mandate is null"));
            return;
        }
        if (mandate.UnderlyingUniverse is null || mandate.EligibleAssetTypes is null ||
            mandate.PermittedDirections is null || mandate.PermittedConditions is null ||
            mandate.PermittedTradeFamilies is null)
            errors.Add(new($"{command.CommandName}.Mandate contains null collections"));
        else
            AddErrors(errors, mandate.Validate(), command.CommandName);
        if (command.EntityId is null)
            return;
        if (mandate.PortfolioId != command.EntityId?.PortfolioId || mandate.FundId != command.EntityId?.FundId)
            errors.Add(new($"{command.CommandName}.Mandate identity does not match EntityId"));
    }

    /// <summary>Checks ValidateOrderId without changing state.</summary>
    static void ValidateOrderId(
        List<ValidationError> errors,
        PortfolioFundOrderId? orderId,
        PortfolioFundId? entityId,
        string commandName)
    {
        if (orderId is null)
        {
            errors.Add(new($"{commandName}.OrderId is null"));
            return;
        }
        AddErrors(errors, orderId.Validate(), commandName);
        if (orderId.PortfolioId != entityId?.PortfolioId || orderId.FundId != entityId?.FundId)
            errors.Add(new($"{commandName}.OrderId parent identity does not match EntityId"));
    }

    /// <summary>Checks ValidateExpectedVersion without changing state.</summary>
    static void ValidateExpectedVersion(List<ValidationError> errors, long expectedVersion, string commandName)
    {
        if (expectedVersion < 0)
            errors.Add(new($"{commandName}.ExpectedVersion cannot be negative"));
    }

    /// <summary>Checks ValidateReason without changing state.</summary>
    static void ValidateReason(List<ValidationError> errors, string? reason, string commandName)
    {
        if (string.IsNullOrWhiteSpace(reason))
            errors.Add(new($"{commandName}.Reason is required"));
    }

    /// <summary>Checks ValidateUtcWindow without changing state.</summary>
    static void ValidateUtcWindow(List<ValidationError> errors, DateTime start, DateTime end, string commandName)
    {
        if (start.Kind != DateTimeKind.Utc || end.Kind != DateTimeKind.Utc || end <= start)
            errors.Add(new($"{commandName}.time window must contain ordered UTC values"));
    }

    /// <summary>Checks AddErrors without changing state.</summary>
    static void AddErrors(List<ValidationError> errors, IEnumerable<string> messages, string commandName)
    {
        foreach (var message in messages)
            errors.Add(new($"{commandName}.{message}"));
    }

}
