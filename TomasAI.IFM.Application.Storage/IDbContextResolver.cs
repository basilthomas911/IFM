using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage
{
    public interface IDbContextResolver
    {
        IObjectRepository<TRepo> Resolve<TRepo>() where TRepo : IObjectRepository;
    }
}
