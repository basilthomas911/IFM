using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Framework.Storage
{
    public interface IDataReaderOptions
    {
        Uri Uri { get; }
        DataReaderType DataReaderType { get; }
        DataSourceType DataSourceType { get; }
        string ApiKey { get; }
    }
}
