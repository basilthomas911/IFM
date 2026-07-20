using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain
{
    public static class DomainAssembly
    {
        public static Assembly Current => Assembly.GetExecutingAssembly();
    }

}
