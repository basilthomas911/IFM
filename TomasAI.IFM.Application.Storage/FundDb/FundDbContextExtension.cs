using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.FundDb
{
    public static class FundDbContextExtension
    {
        public static IFundDbReadContext Query(this IObjectRepository<FundDbContext> db) => db as IFundDbReadContext;
    }
}
