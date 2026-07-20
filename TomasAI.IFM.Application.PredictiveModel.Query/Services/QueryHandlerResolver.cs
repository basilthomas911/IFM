using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.PredictiveModel.Query.Services
{
    public class QueryHandlerResolver : IQueryHandlerResolver
    {
        private Func<Type, object> _resolverFunction;

        /// <summary>
        /// create query handler resolver
        /// </summary>
        /// <param name="resolverFunction">function that will return query handler using dependancy injection</param>
        public QueryHandlerResolver(Func<Type, object> resolverFunction)
            => _resolverFunction = resolverFunction;

        /// <summary>
        /// call resolver function to return query handler from depedancy injection container
        /// </summary>
        /// <param name="queryHandlerType"></param>
        /// <returns></returns>
        public object Resolve(Type queryHandlerType)
        {
            var qryHandler = default(object);
            try
            {
                qryHandler = _resolverFunction(queryHandlerType);
            }
            catch { }
            return qryHandler;
        }
    }

}