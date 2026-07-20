using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Event.SignalR.Services
{ 
    public interface IEventHandlerResolver
    {
        object[] Resolve(Type eventHandlerType);
    }
}
