using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage
{
    public interface IStorageUrlSettings
    {
        IStorageUrlSetting this[string connectionName] { get; }
        int Count { get; }
        IStorageUrlSettings Add(string storgeName, string storageUrl);
    }
}
