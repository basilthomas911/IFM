using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace TomasAI.IFM.Shared
{
    public static class SharedDomainAssembly
    {
        public static Assembly Current => Assembly.GetExecutingAssembly();
    }
}
