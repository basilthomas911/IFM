using System;
using TomasAI.IFM.Shared.EventSourcing;


namespace TomasAI.IFM.Shared.Domain
{
    public class ValidationDecoratorFactory : IValidationDecoratorFactory
    {
        readonly Func<Type, object> _decoratorResolver;

        public ValidationDecoratorFactory(Func<Type, object> decoratorResolver = null)
        {
            _decoratorResolver = decoratorResolver;
        }

        public IValidationCommandDecorator<TState> GetDecorator<TState>() where TState : IBoundedContextState
        {
            var decoratorType = typeof(IValidationCommandDecorator<>).MakeGenericType(typeof(TState));
            return _decoratorResolver?.Invoke(decoratorType) as IValidationCommandDecorator<TState>;
        }
    }
}
