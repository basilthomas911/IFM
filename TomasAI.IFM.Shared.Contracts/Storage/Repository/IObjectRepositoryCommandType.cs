using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Storage
{
    public interface IObjectRepositoryCommandType
    {
        CommandType CommandType { get; }
    }
}
