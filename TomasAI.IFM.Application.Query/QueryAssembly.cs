using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Query
{
    public static class QueryAssembly
    {
        public static Assembly Current => Assembly.GetExecutingAssembly();
    }

}
