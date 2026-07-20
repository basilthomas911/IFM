using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.SignalRClient;

namespace TomasAI.IFM.SystemAdmin.SignalRClient
{
    public class BackupServiceListenerOptions : SignalRServiceListenerOptions, IBackupServiceListenerOptions
    {

        public BackupServiceListenerOptions(string baseUri) 
            : base(baseUri)
        {
        }

    }
}
