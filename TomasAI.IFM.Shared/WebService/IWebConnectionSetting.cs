using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public interface IWebConnectionSetting
    {
        string Name { get; }
        string BaseUri { get; }
    }
}
