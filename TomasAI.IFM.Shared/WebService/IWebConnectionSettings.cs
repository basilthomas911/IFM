using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public interface IWebConnectionSettings
    {
        IWebConnectionSetting this[string connectionName] { get; }
        int Count { get; }
        IWebConnectionSettings Add(string connectionName, string webAddress);
    }
}
