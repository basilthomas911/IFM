using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.SystemAdmin.Shared;

namespace TomasAI.IFM.Application.Storage.TradePlanDb
{
    public interface ITradePlanDbContext
    {
        ITradePlanDbReadContext DbReader { get; }
        ITradePlanDbWriteContext DbWriter { get; }
    }
}
