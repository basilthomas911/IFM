using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Event.SignalR.Services
{
    public static class ServiceExtensions
    {
        public static async Task SendAsync(this IClientProxy clientProxy , IEvent @event)
        {
            var methodName = @event.GetType().Name.Replace("Event", "");
            await clientProxy.SendAsync(methodName, @event);
        }
    }
}
