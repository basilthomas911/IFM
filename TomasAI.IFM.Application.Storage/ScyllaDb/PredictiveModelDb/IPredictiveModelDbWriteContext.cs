using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.PredictiveModelDb
{
    public interface IPredictiveModelDbWriteContext
    {
        Task CreatePredictiveModelDbTablesAsync();
    }
}
