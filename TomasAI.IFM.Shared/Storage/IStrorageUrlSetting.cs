using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage
{
    public interface IStorageUrlSetting
    {
        string Name { get; }
        string StorageUrl { get; }
    }
}
