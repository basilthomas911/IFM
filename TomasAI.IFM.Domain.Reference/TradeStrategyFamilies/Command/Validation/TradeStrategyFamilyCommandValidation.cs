using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command.Validation;

/// <summary>Checks catalog and legacy Family command payloads before dispatch.</summary>
public static class TradeStrategyFamilyCommandValidation
{
    /// <summary>Validates the bounded JSON contract, operation identity and catalog operation.</summary>
    public static List<ValidationError> ValidateStrategyCatalogCommand(this List<ValidationError> errors, StrategyCatalogCommand command)
    {
        CatalogCommandRequest? request = null;
        if (string.IsNullOrWhiteSpace(command.RequestJson) || System.Text.Encoding.UTF8.GetByteCount(command.RequestJson) > 524288)
            errors.Add(new("RequestJson must contain a catalog command within 524288 bytes."));
        else
        {
            try { request = StrategyCatalogJson.Read<CatalogCommandRequest>(command.RequestJson); }
            catch (JsonException exception) { errors.Add(new($"RequestJson is invalid: {exception.Message}")); }
            catch (ArgumentException exception) { errors.Add(new($"RequestJson is invalid: {exception.Message}")); }
        }
        if (request is not null)
        {
            if (request.OperationId != command.CommandId) errors.Add(new("Catalog OperationId must match CommandId."));
            if (!Enum.IsDefined(request.Operation)) errors.Add(new("Catalog Operation is invalid."));
            if (request.ExpectedPreviousVersion < 0) errors.Add(new("ExpectedPreviousVersion cannot be negative."));
            if (request.EffectiveUtc is { Kind: not DateTimeKind.Utc }) errors.Add(new("EffectiveUtc must be UTC."));
            if (request.Operation == CatalogCommandOperation.SaveDraft && request.Definition is null) errors.Add(new("Definition is required for SaveDraft."));
            if (request.Operation is CatalogCommandOperation.Publish or CatalogCommandOperation.Retire && request.Key is null)
                errors.Add(new("An exact catalog Key is required."));
        }
        return errors.ValidateCatalogIdentity(command);
    }

    /// <summary>Validates all legacy create request fields.</summary>
    public static List<ValidationError> ValidateStrategyCatalogCommand(this List<ValidationError> errors, CreateTradeStrategyFamilyCommand command)
    {
        Append(errors, command.Request?.Validate(), command.CommandName);
        if (command.Request is not null && command.Request.OperationId != command.CommandId) errors.Add(new("Request.OperationId must match CommandId."));
        return errors.ValidateCatalogIdentity(command);
    }

    /// <summary>Validates all legacy change request fields and exact version references.</summary>
    public static List<ValidationError> ValidateStrategyCatalogCommand(this List<ValidationError> errors, ChangeTradeStrategyFamilyCommand command)
    {
        Append(errors, command.Request?.Validate(), command.CommandName);
        if (command.Request is not null && command.Request.OperationId != command.CommandId) errors.Add(new("Request.OperationId must match CommandId."));
        return errors.ValidateCatalogIdentity(command);
    }

    /// <summary>Validates the legacy remove operation and exact Family reference.</summary>
    public static List<ValidationError> ValidateStrategyCatalogCommand(this List<ValidationError> errors, RemoveTradeStrategyFamilyCommand command)
    {
        Append(errors, command.Request?.Validate(), command.CommandName);
        if (command.Request is not null && command.Request.OperationId != command.CommandId) errors.Add(new("Request.OperationId must match CommandId."));
        return errors.ValidateCatalogIdentity(command);
    }

    /// <summary>Checks that the subject retains the same catalog actor entity.</summary>
    private static List<ValidationError> ValidateCatalogIdentity(this List<ValidationError> errors, ICommand<ActorEntityId> command)
    {
        if (command.Subject.EntityId != command.EntityId.Format()) errors.Add(new("Subject.EntityId must match EntityId."));
        return errors;
    }

    /// <summary>Appends every request failure, including a missing request.</summary>
    private static void Append(List<ValidationError> errors, IReadOnlyList<string>? failures, string name)
    {
        if (failures is null) errors.Add(new($"{name}.Request is required."));
        else errors.AddRange(failures.Select(failure => new ValidationError($"{name}.Request: {failure}")));
    }
}
