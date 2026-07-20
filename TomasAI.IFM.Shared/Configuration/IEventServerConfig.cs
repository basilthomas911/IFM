using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Configuration
{
    public interface IEventServerConfig
    {
        string CommandHubBaseUri { get; }
    }
}
