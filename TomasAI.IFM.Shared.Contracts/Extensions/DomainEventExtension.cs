using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Extensions
{
    public static class DomainEventExtension
    {

        extension(IEvent domainEvent)
        {
            public string ToEventData() => JsonConvert.SerializeObject(domainEvent, Formatting.Indented);
        }
    }
}
