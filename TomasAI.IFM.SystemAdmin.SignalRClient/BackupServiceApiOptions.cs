using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.SystemAdmin.SignalRClient
{
    public class BackupServiceApiOptions : SignalRServiceApiOptions, IBackupServiceApiOptions
    {
        public BackupServiceApiOptions(string baseUri):base(baseUri)
        {
        }
    }
}
