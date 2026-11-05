using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;

/// <summary>Commits accepted import rows and the destination command facts in one PostgreSQL transaction.</summary>
public static class ApplyEconomicCalendarsImport
{
    extension(ApplyEconomicCalendarsImportCommand command)
    {
        public async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(IPostgresEventTransaction transactions)
        {
            var source = command.Accepted;
            var rows = source?.EconomicCalendars;
            if (source is null || rows is null || source.CommandId == Guid.Empty || source.Id != command.CommandId ||
                source.EntityId != command.EntityId || !Enum.IsDefined(source.DuplicatePolicy))
                return new ServiceFailed<GuidResult>(command.ErrorCode, "Invalid acquired MarketData import evidence.");

            string Stream(string entity) => new ActorSubject(
                ActorType.Command, ImportEconomicCalendarsCommand.Actor, ApplyEconomicCalendarsImportCommand.Verb, entity).StreamId;
            var groups = rows.GroupBy(row => row.Id.Format(), StringComparer.Ordinal).ToArray();
            var acceptedStream = "market-data/economic-calendar/accepted/" + source.CommandId.ToString("N");
            await transactions.ExecuteOnceAsync(async (transaction, token) =>
            {
                var acceptedHead = await MarketDataImportStreamFence.LockAsync(transaction, [acceptedStream],
                    "EconomicCalendarEventProjector", token).ConfigureAwait(false);
                if (acceptedHead[acceptedStream] != 0)
                    return true;

                var heads = await MarketDataImportStreamFence.LockAsync(transaction, groups.Select(group => Stream(group.Key)),
                    "EconomicCalendarEventProjector", token).ConfigureAwait(false);
                await transaction.ImportEconomicCalendarsAsync(rows, source.DuplicatePolicy, source.CommandId, token).ConfigureAwait(false);
                foreach (var group in groups)
                {
                    var stream = Stream(group.Key);
                    var version = heads[stream];
                    foreach (var row in group)
                    {
                        var changed = new EconomicCalendarChangedEvent
                        {
                            Id = Guid.NewGuid(), CommandId = command.CommandId, EntityId = row.Id,
                            Subject = new(ActorType.Event, EconomicCalendarChangedEvent.Actor, EconomicCalendarChangedEvent.Verb, group.Key),
                            EventSource = command.EventSource, ReceivedOn = source.ReceivedOn, UpdatedOn = source.ReceivedOn,
                            UpdatedBy = command.EventSource, EconomicCalendar = row
                        };
                        await transaction.AppendAsync(stream, command.CommandId, changed, version++, token).ConfigureAwait(false);
                    }
                }
                await transaction.AppendAsync(acceptedStream, source.CommandId, source, 0, token).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        }
    }
}
