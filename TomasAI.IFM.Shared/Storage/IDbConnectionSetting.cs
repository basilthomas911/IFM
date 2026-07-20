using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage
{
    public interface IDbConnectionSetting
    {
        string Name { get; }
        string ConnectionString { get; }
        string ProviderName { get; }
    }
}
