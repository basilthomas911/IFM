using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage.Azure
{
    public interface IAzureStorage
    {
        Task UploadFileAsync(string dbName, string backupType, Func<string,Task>? progressFunc = null);
    }


}
