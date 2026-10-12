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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056", Justification = "The public configuration contract accepts textual endpoint/storage addresses and preserves binding compatibility.")]
        string StorageUrl { get; }
    }
}
