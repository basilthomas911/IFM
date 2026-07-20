using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.PredictiveModelDb
{
    public interface IPredictiveModelDbContext : IPredictiveModelDbWriteContext, IPredictiveModelDbReadContext
    {
        IPredictiveModelDbReadContext DbReader { get; }
        IPredictiveModelDbWriteContext DbWriter { get; }
    }
}
