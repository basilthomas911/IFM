using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventSourcing
{
    public interface ICommandHandlerResolver
    {
        object Resolve(Type cmdHandlerType);
    }
}
