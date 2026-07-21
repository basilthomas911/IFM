using System;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.Extensions
{
    /// <summary>
    /// throw argument null exception if argument is null
    /// </summary>
    public static class IsArgumentNull
    {
        public static TResult Set<TResult>(TResult arg, [CallerArgumentExpression("arg")] string? argName = null)
        {
            if (arg is null) throw new ArgumentNullException(argName);
            return arg;
        }

        public static void Check<TResult>(TResult arg, [CallerArgumentExpression("arg")] string? argName = null)
        { 
            if (arg is null)
                throw new ArgumentNullException(argName);
        }

        public static void Check(string arg, [CallerArgumentExpression("arg")] string? argName = null)
        {
            if (string.IsNullOrWhiteSpace(arg))
                throw new ArgumentNullException(argName);
        }

        public static void Check(DateTime arg, [CallerArgumentExpression("arg")] string? argName = null)
        {
            if (arg == DateTime.MinValue || arg == DateTime.MaxValue)
                throw new ArgumentNullException(argName);
        }

        public static void Check(ActorThreadId threadId, [CallerArgumentExpression("threadId")] string? argName = null)
        {
            if (string.IsNullOrEmpty(threadId.Name))
                throw new ArgumentNullException(argName);
        }

    }
}

/*
namespace TomasAI.IFM.Shared.Extensions
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }
}
*/
