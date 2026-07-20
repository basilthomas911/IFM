using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventSourcing;

public interface IAsyncQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> ExecuteAsync(TQuery qryParam);
}

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    TResult Execute(TQuery qryParam);
}
