using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Validation;

/// <summary>Download-log hash and duplicated-identity checks after intrinsic payload validation.</summary>
public static class DownloadLogCommandValidation
{
    /// <summary>Validates the serialized hash and cross-checks the command envelope against valid outcome evidence.</summary>
    public static TErrors ValidateDownloadLogEnvelope<TErrors>(this TErrors errors, InsertMarketDataDownloadLogCommand command)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(errors);
        if (string.IsNullOrWhiteSpace(command.PayloadSha256))
            errors.Add(new ValidationError("DownloadLog.PayloadSha256 is required."));
        if (command.Outcome is not { } outcome || new List<ValidationError>().ValidateDownloadOutcome(outcome).Count != 0)
            return errors;
        if (command.EntityId is not null && command.EntityId.ImportCommandId != outcome.ImportCommandId)
            errors.Add(new ValidationError("DownloadLog.EntityId does not match Outcome.ImportCommandId."));
        if (command.CommandId != MarketDataDownloadOutcome.LoggingCommandId(outcome.ImportCommandId))
            errors.Add(new ValidationError("DownloadLog.CommandId does not match the deterministic outcome logging identity."));
        if (!string.Equals(command.PayloadSha256,outcome.ComputeHash(),StringComparison.Ordinal))
            errors.Add(new ValidationError("DownloadLog.PayloadSha256 does not match Outcome."));
        if (command.RouteTo != BoundedContextName.DownloadLogBoundedContext ||
            command.Subject.ActorType != ActorType.Command || !string.Equals(command.Subject.Name,InsertMarketDataDownloadLogCommand.Actor,StringComparison.Ordinal) ||
            !string.Equals(command.Subject.Verb,InsertMarketDataDownloadLogCommand.Verb,StringComparison.Ordinal) || !string.Equals(command.Subject.EntityId,command.EntityId?.Format(),StringComparison.Ordinal))
            errors.Add(new ValidationError("DownloadLog route does not match its command identity."));
        return errors;
    }
}


