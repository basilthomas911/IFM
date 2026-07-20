using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Extensions
{
    public static class DomainEventExtension
    {
        public static string ToEventData(this IEvent domainEvent) => JsonConvert.SerializeObject(domainEvent, Formatting.Indented);
    }
}
