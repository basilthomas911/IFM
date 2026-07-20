using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace TomasAI.IFM.Application.Command
{
    public static class AppCommandAssembly
    {
        public static Assembly Current => Assembly.GetExecutingAssembly();
    }
}
