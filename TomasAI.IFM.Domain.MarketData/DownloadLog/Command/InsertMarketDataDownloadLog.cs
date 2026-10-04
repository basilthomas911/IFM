using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Model;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command;

/// <summary>Records a terminal download outcome through computation, guards and state-owned application.</summary>
public static class InsertMarketDataDownloadLog
{
    /// <summary>Records a valid outcome once; acknowledges exact replay and rejects conflicting terminal evidence.</summary>
    /// <param name="command">The terminal download logging intent.</param>
    /// <param name="state">The authoritative download-log state.</param>
    /// <returns>The command ID on success, or the conflicting-outcome/application failure.</returns>
    /// <exception cref="ArgumentException">The command identity, route or hash does not match its outcome.</exception>
    public static ServiceResult<GuidResult> Execute(this InsertMarketDataDownloadLogCommand command, DownloadLogCommandState state)
    {
        var errorMsg = "DownloadLog.STATE.APPLY_FAILED";
        var computed = command.Compute(out var downloadLogEntry);
        if (computed && downloadLogEntry.Matches(state.Outcome, state.PayloadSha256))
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var updated = computed switch
        {
            _ when !computed => command.UpdateFailed(ref errorMsg, "DownloadLog.COMPUTED_OUTCOME.INVALID"),
            _ when state.Outcome is not null
                => command.UpdateFailed(ref errorMsg, "DownloadLog.TERMINAL_OUTCOME.CONFLICT; A different terminal outcome is already committed for this import attempt."),
            _ => state.Update(command.CreateMarketDataDownloadLogInsertedEvent(downloadLogEntry), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes guarded terminal evidence without changing command or actor state.</summary>
    /// <param name="command">The download logging intent.</param>
    /// <param name="downloadLogEntry">The computed terminal outcome and content hash.</param>
    /// <returns>True when computed evidence belongs to the commanded import attempt.</returns>
    /// <exception cref="ArgumentException">The command identity, route or hash is invalid.</exception>
    internal static bool Compute(this InsertMarketDataDownloadLogCommand command, out DownloadLogEntry downloadLogEntry)
    {
        command.Validate();
        downloadLogEntry = new(command.Outcome with { }, command.PayloadSha256);
        return downloadLogEntry.Outcome.ImportCommandId == command.EntityId.ImportCommandId;
    }

    /// <summary>Creates the source event from guarded terminal evidence without changing state.</summary>
    /// <param name="command">The originating command and route.</param>
    /// <param name="downloadLogEntry">The computed outcome and content hash.</param>
    /// <returns>The private event applied and persisted through State.Update.</returns>
    internal static MarketDataDownloadLogInsertedEvent CreateMarketDataDownloadLogInsertedEvent(
        this InsertMarketDataDownloadLogCommand command, DownloadLogEntry downloadLogEntry) => new()
    {
        CommandId = command.CommandId,
        EntityId = command.EntityId,
        Subject = new(ActorType.Event, MarketDataDownloadLogInsertedEvent.Actor,
            MarketDataDownloadLogInsertedEvent.Verb, command.EntityId.Format()),
        Outcome = downloadLogEntry.Outcome,
        PayloadSha256 = downloadLogEntry.PayloadSha256
    };
    /// <summary>Checks committed terminal evidence when the command audit identifies a duplicate.</summary>
    /// <param name="command">The logging command delivered again.</param>
    /// <param name="repository">The authoritative download-state repository.</param>
    /// <param name="cancellationToken">Cancellation for state reconstruction.</param>
    /// <returns>True when no outcome is committed and the logging intent still needs execution.</returns>
    /// <exception cref="InvalidOperationException">A different terminal outcome is already committed.</exception>
    internal static async ValueTask<bool> ShouldProcessDuplicateAsync(this InsertMarketDataDownloadLogCommand command,
        TomasAI.IFM.Shared.EventModelActor.Contracts.IEventSourceActorStateRepository<DownloadLogCommandState> repository,
        CancellationToken cancellationToken)
    {
        var state = await repository.LoadStateAsync(command, cancellationToken).ConfigureAwait(false);
        return !state.VerifyDuplicate(command);
    }
}
