using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage
{
    public interface IObjectBulkCopyContext
    {
        void BulkCopy();
        Task BulkCopyAsync();
    }
}
