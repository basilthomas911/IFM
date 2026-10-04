using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Validation;

/// <summary>Download-log hash and duplicated-identity checks after intrinsic payload validation.</summary>
public static class DownloadLogCommandValidation
{
    /// <summary>Validates the serialized hash and cross-checks the command envelope against valid outcome evidence.</summary>
    public static List<ValidationError> ValidateDownloadLogEnvelope(this List<ValidationError> errors, InsertMarketDataDownloadLogCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.PayloadSha256))
            errors.Add(new ValidationError("DownloadLog.PayloadSha256 is required."));
        if (command.Outcome is not { } outcome || new List<ValidationError>().ValidateDownloadOutcome(outcome).Count != 0)
            return errors;
        if (command.EntityId is not null && command.EntityId.ImportCommandId != outcome.ImportCommandId)
            errors.Add(new ValidationError("DownloadLog.EntityId does not match Outcome.ImportCommandId."));
        if (command.CommandId != MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId))
            errors.Add(new ValidationError("DownloadLog.CommandId does not match the deterministic outcome logging identity."));
        if (command.PayloadSha256 != outcome.ComputeHash())
            errors.Add(new ValidationError("DownloadLog.PayloadSha256 does not match Outcome."));
        if (command.RouteTo != BoundedContextName.DownloadLogBoundedContext ||
            command.Subject.ActorType != ActorType.Command || command.Subject.Name != InsertMarketDataDownloadLogCommand.Actor ||
            command.Subject.Verb != InsertMarketDataDownloadLogCommand.Verb || command.Subject.EntityId != command.EntityId?.Format())
            errors.Add(new ValidationError("DownloadLog route does not match its command identity."));
        return errors;
    }
}


