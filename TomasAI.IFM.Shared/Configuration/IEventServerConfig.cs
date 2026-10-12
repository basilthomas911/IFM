using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Configuration
{
    public interface IEventServerConfig
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056", Justification = "The public configuration contract accepts textual endpoint/storage addresses and preserves binding compatibility.")]
        string CommandHubBaseUri { get; }
    }
}
