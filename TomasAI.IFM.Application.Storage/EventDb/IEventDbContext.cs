using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin;

namespace TomasAI.IFM.Application.Storage.EventDb
{
    public interface IEventDbContext
    {
        Task<long> GetEntityIdAsync(string entityIdValue);
        Task<DomainEventCollection> LoadEventsAsync<TAggregateRoot>(long entityId) where TAggregateRoot : IAggregateRoot;
        Task<DomainEventCollection> LoadEventsAsync<TAggregateRoot, TEvent>(long entityId, int lastNRange) where TAggregateRoot : IAggregateRoot where TEvent : IEvent;
        Task<DomainEventCollection> LoadEventsAsync<TAggregateRoot, TSnapshot>(long entityId) where TAggregateRoot : IAggregateRoot where TSnapshot : IEvent;
        Task<DomainEventCollection> SaveEventsAsync(Type aggStateType, long entityId, DomainEventCollection domainEvents, ICommand command, Action<EventSource> eventSourceAction = null);
        Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage);
        Task DeleteEventLogAsync();
    }
}
