using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Model;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;

/// <summary>Records a guarded import request; the projector performs downloading and storage policy enforcement.</summary>
public static class ImportYieldCurveRates
{
    /// <summary>Computes request parameters and records one operation marker without rebuilding external records.</summary>
    /// <param name="command">The import intent and duplicate policy.</param>
    /// <param name="state">The authoritative command state.</param>
    /// <returns>The command ID on success, or the request/application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this ImportYieldCurveRatesCommand command, YieldCurveRateCommandState state)
    {
        var errorMsg = "YieldCurveRate.IMPORT.STATE.APPLY_FAILED";
        var computed = command.Compute(out var yieldCurveRateImport);
        var updated = computed switch
        {
            _ when !computed => command.UpdateFailed(ref errorMsg, "YieldCurveRate.IMPORT.REQUEST.INVALID"),
            _ => state.Update(command.CreateYieldCurveRatesImportedEvent(yieldCurveRateImport), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable request parameters without downloading data or changing state.</summary>
    /// <param name="command">The import intent.</param>
    /// <param name="yieldCurveRateImport">The computed request parameters.</param>
    /// <returns>True when required request parameters and ownership are valid.</returns>
    internal static bool Compute(this ImportYieldCurveRatesCommand command, out YieldCurveRateImport yieldCurveRateImport)
    {
        yieldCurveRateImport = new(command.ImportDate, command.DuplicatePolicy);
        return command.EntityId is not null && yieldCurveRateImport.ImportDate != default && Enum.IsDefined(yieldCurveRateImport.DuplicatePolicy) && yieldCurveRateImport.ImportDate.Year == command.EntityId.Year;
    }

    /// <summary>Creates the source event carrying the guarded import request parameters.</summary>
    /// <param name="command">The originating command and route.</param>
    /// <param name="yieldCurveRateImport">The computed import request.</param>
    /// <returns>The private request event persisted through State.Update.</returns>
    internal static YieldCurveRatesImportedEvent CreateYieldCurveRatesImportedEvent(this ImportYieldCurveRatesCommand command, YieldCurveRateImport yieldCurveRateImport) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, YieldCurveRatesImportedEvent.Actor, YieldCurveRatesImportedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        ImportDate = yieldCurveRateImport.ImportDate,
        DuplicatePolicy = yieldCurveRateImport.DuplicatePolicy,
        RequestedOn = command.OriginatedOn,
        RequestedBy = command.OriginatedBy
    };
}
