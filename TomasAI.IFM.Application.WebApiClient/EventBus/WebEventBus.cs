using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Fund.WebApi;
using TomasAI.IFM.Shared.WebService;
using TomasAI.IFM.Shared.EventSourcing;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.WebApiClient.EventBus
{
    public class WebEventBus : RestApiService, IEventBus
    {
        public WebEventBus(IWebConnectionSettings connectionSettings)
            : this(connectionSettings["WebEventBus"])
        {
        }

        private WebEventBus(IWebConnectionSetting connectionSetting)
            : base(new Uri(connectionSetting.BaseUri))
        {
        }

        public void Publish(object sender, IDomainEvent[] events)
            => Task.Run(() => Post<IDomainEvent[]>("EventBus", events) );

        public async Task PublishAsync(object sender, IDomainEvent[] events)
        {
            var result = await PostAsync<IDomainEvent[]>("EventBus", events);
            if (!result.Success)
                throw new ServiceException(result.ErrorCode, result.ErrorMessage);
        }

        public Task PublishEventsAsync(DomainEventCollection events)
        {
            throw new NotImplementedException();
        }
    }
}
