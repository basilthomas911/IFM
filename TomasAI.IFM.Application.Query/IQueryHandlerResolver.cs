using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Query
{
    public interface IQueryHandlerResolver
    {
        object? Resolve(Type queryHandlerType);
    }
}
