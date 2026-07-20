using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace TomasAI.IFM.Service.AlgoTrader.Model
{
    public static class AlgoTraderAssembly
    {
        public static Assembly Current => Assembly.GetExecutingAssembly();
    }
}
