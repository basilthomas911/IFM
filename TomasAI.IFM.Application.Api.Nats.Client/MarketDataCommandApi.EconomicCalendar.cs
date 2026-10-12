using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public partial class MarketDataCommandApi
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Typed client transport boundary converts failures to the declared ServiceResult via OnError rather than throwing across the API contract.")]
    public async Task<ServiceResult<Guid>> AddEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar)
    {
        ArgumentNullException.ThrowIfNull(economicCalendar);
        var commandId = Guid.NewGuid();
        try
        {
            IsArgumentNull.Check(economicCalendar);
            var entityId = economicCalendar.Id;
            var command = new AddEconomicCalendarCommand(economicCalendar)
            {
                CommandId = commandId,
                Subject = new ActorSubject(ActorType.Command, AddEconomicCalendarCommand.Actor, AddEconomicCalendarCommand.Verb, entityId.Format()),
                ErrorCode = AddEconomicCalendarCommand.ErrorId
            };
            return await (RequestCommandAsync(command, entityId)).ConfigureAwait(false);
        }
        catch (Exception ex) { return OnError(ex, commandId, AddEconomicCalendarCommand.ErrorId); }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Typed client transport boundary converts failures to the declared ServiceResult via OnError rather than throwing across the API contract.")]
    public async Task<ServiceResult<Guid>> ChangeEconomicCalendarAsync(EconomicCalendarId economicCalendarId, EconomicCalendarReadModel economicCalendar, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(economicCalendarId);
        var commandId = Guid.NewGuid();
        try
        {
            var command = new ChangeEconomicCalendarCommand(economicCalendarId, economicCalendar, overwrite)
            {
                CommandId = commandId,
                Subject = new ActorSubject(ActorType.Command, ChangeEconomicCalendarCommand.Actor, ChangeEconomicCalendarCommand.Verb, economicCalendarId.Format()),
                ErrorCode = ChangeEconomicCalendarCommand.ErrorId
            };
            return await (RequestCommandAsync(command, command.EntityId)).ConfigureAwait(false);
        }
        catch (Exception ex) { return OnError(ex, commandId, ChangeEconomicCalendarCommand.ErrorId); }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Typed client transport boundary converts failures to the declared ServiceResult via OnError rather than throwing across the API contract.")]
    public async Task<ServiceResult<Guid>> RemoveEconomicCalendarAsync(EconomicCalendarId economicCalendarId, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(economicCalendarId);
        var commandId = Guid.NewGuid();
        try
        {
            var command = new RemoveEconomicCalendarCommand(economicCalendarId, overwrite)
            {
                CommandId = commandId,
                Subject = new ActorSubject(ActorType.Command, RemoveEconomicCalendarCommand.Actor, RemoveEconomicCalendarCommand.Verb, economicCalendarId.Format()),
                ErrorCode = RemoveEconomicCalendarCommand.ErrorId
            };
            return await (RequestCommandAsync(command, command.EntityId)).ConfigureAwait(false);
        }
        catch (Exception ex) { return OnError(ex, commandId, RemoveEconomicCalendarCommand.ErrorId); }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Typed client transport boundary converts failures to the declared ServiceResult via OnError rather than throwing across the API contract.")]
    public async Task<ServiceResult<Guid>> ImportEconomicCalendarsAsync(DateTime importedDate, string[]? countryCodes = null)
    {
        var commandId = Guid.NewGuid();
        try
        {
            var entityId = new EconomicCalendarId(importedDate, "ZZ", "ImportEconomicCalendars");
            var command = new ImportEconomicCalendarsCommand(importedDate, countryCodes)
            {
                CommandId = commandId,
                Subject = new ActorSubject(ActorType.Command, ImportEconomicCalendarsCommand.Actor, ImportEconomicCalendarsCommand.Verb, entityId.Format()),
                ErrorCode = ImportEconomicCalendarsCommand.ErrorId
            };
            return await (RequestCommandAsync(command, entityId)).ConfigureAwait(false);
        }
        catch (Exception ex) { return OnError(ex, commandId, ImportEconomicCalendarsCommand.ErrorId); }
    }
}
