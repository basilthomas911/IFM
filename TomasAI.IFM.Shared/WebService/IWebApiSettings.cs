using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public interface IWebApiSettings
    {
        Uri WebApiCommandUri { get; }
        Uri WebApiQueryUri { get; }
        int Count { get; }

        IWebApiSettings Add(string uriName, Uri uriValue);
    }
}
