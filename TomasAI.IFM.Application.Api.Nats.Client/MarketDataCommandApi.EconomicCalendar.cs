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
    public async Task<ServiceResult<Guid>> AddEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar)
    {
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
            return await RequestCommandAsync(command, entityId);
        }
        catch (Exception ex) { return OnError(ex, commandId, AddEconomicCalendarCommand.ErrorId); }
    }

    public async Task<ServiceResult<Guid>> ChangeEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel economicCalendar, bool overwrite)
    {
        var commandId = Guid.NewGuid();
        try
        {
            var command = new ChangeEconomicCalendarCommand(id, economicCalendar, overwrite)
            {
                CommandId = commandId,
                Subject = new ActorSubject(ActorType.Command, ChangeEconomicCalendarCommand.Actor, ChangeEconomicCalendarCommand.Verb, id.Format()),
                ErrorCode = ChangeEconomicCalendarCommand.ErrorId
            };
            return await RequestCommandAsync(command, command.EntityId);
        }
        catch (Exception ex) { return OnError(ex, commandId, ChangeEconomicCalendarCommand.ErrorId); }
    }

    public async Task<ServiceResult<Guid>> RemoveEconomicCalendarAsync(EconomicCalendarId id, bool overwrite)
    {
        var commandId = Guid.NewGuid();
        try
        {
            var command = new RemoveEconomicCalendarCommand(id, overwrite)
            {
                CommandId = commandId,
                Subject = new ActorSubject(ActorType.Command, RemoveEconomicCalendarCommand.Actor, RemoveEconomicCalendarCommand.Verb, id.Format()),
                ErrorCode = RemoveEconomicCalendarCommand.ErrorId
            };
            return await RequestCommandAsync(command, command.EntityId);
        }
        catch (Exception ex) { return OnError(ex, commandId, RemoveEconomicCalendarCommand.ErrorId); }
    }

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
            return await RequestCommandAsync(command, entityId);
        }
        catch (Exception ex) { return OnError(ex, commandId, ImportEconomicCalendarsCommand.ErrorId); }
    }
}
