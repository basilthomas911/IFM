using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;


namespace TomasAI.IFM.Shared.Domain
{
    public class ExceptionDecoratorFactory : IExceptionDecoratorFactory
    {
        private readonly Func<Type, object> _decoratorResolver;
        public ExceptionDecoratorFactory(Func<Type, object> decoratorResolver = null)
        {
            _decoratorResolver = decoratorResolver;
        }

        public object GetDecorator<TState>() where TState : IBoundedContextState
        {
            var decoratorType = typeof(IExceptionCommandDecorator<>).MakeGenericType(typeof(TState));
            return _decoratorResolver?.Invoke(decoratorType);
        }
    }
}
