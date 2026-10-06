using System;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Shared.Extensions
{
    public static class LoggerExtension
    {
        public static void LogInformationEvent(
            this ILogger logger,
            string serviceId,
            string messageTemplate,
            params object[] propertyValues)
        {
            if (!logger.IsEnabled(LogLevel.Information)) return;
            var props = new object[propertyValues.Length + 1];
            props[0] = serviceId;
            Array.Copy(propertyValues, 0, props, 1, propertyValues.Length);
            logger.LogInformation("{ServiceId:l}:  " + messageTemplate, props);
        }

        public static void LogInformationEvent<T0>(
            this ILogger logger,
            string serviceId,
            string messageTemplate,
            T0 arg0)
        {
            if (!logger.IsEnabled(LogLevel.Information)) return;
            logger.LogInformation("{ServiceId:l}:  " + messageTemplate, serviceId, arg0);
        }

        public static void LogInformationEvent<T0, T1>(
            this ILogger logger,
            string serviceId,
            string messageTemplate,
            T0 arg0,
            T1 arg1)
        {
            if (!logger.IsEnabled(LogLevel.Information)) return;
            logger.LogInformation("{ServiceId:l}:  " + messageTemplate, serviceId, arg0, arg1);
        }

        public static void LogErrorEvent(
           this ILogger logger,
           string serviceId,
           Exception errorException,
           string messageTemplate,
           params object[] propertyValues)
        {
            if (!logger.IsEnabled(LogLevel.Error)) return;
            var props = new object[propertyValues.Length + 1];
            props[0] = serviceId;
            Array.Copy(propertyValues, 0, props, 1, propertyValues.Length);
            logger.LogError(errorException, "{ServiceId:l}:  " + messageTemplate, props);
        }

        public static void LogErrorEvent<T0>(
           this ILogger logger,
           string serviceId,
           Exception errorException,
           string messageTemplate,
           T0 arg0)
        {
            if (!logger.IsEnabled(LogLevel.Error)) return;
            logger.LogError(errorException, "{ServiceId:l}:  " + messageTemplate, serviceId, arg0);
        }

        public static void LogErrorEvent(
           this ILogger logger,
           string serviceId,
           string messageTemplate,
           params object[] propertyValues)
        {
            if (!logger.IsEnabled(LogLevel.Error)) return;
            var props = new object[propertyValues.Length + 1];
            props[0] = serviceId;
            Array.Copy(propertyValues, 0, props, 1, propertyValues.Length);
            logger.LogError("{ServiceId:l}:  " + messageTemplate, props);
        }

        public static void LogErrorEvent<T0>(
           this ILogger logger,
           string serviceId,
           string messageTemplate,
           T0 arg0)
        {
            if (!logger.IsEnabled(LogLevel.Error)) return;
            logger.LogError("{ServiceId:l}:  " + messageTemplate, serviceId, arg0);
        }
    }
}
