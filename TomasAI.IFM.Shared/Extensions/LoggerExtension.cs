using System;
using System.Linq;
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
            var props = new object[] { serviceId }.Concat(propertyValues).ToArray();
            logger.LogInformation("{ServiceId:l}:  " + messageTemplate, props);
        }

        public static void LogInformationEvent<T0>(
            this ILogger logger,
            string serviceId,
            string messageTemplate,
            T0 arg0)
        {
            logger.LogInformation("{ServiceId:l}:  " + messageTemplate, serviceId, arg0);
        }

        public static void LogInformationEvent<T0, T1>(
            this ILogger logger,
            string serviceId,
            string messageTemplate,
            T0 arg0,
            T1 arg1)
        {
            logger.LogInformation("{ServiceId:l}:  " + messageTemplate, serviceId, arg0, arg1);
        }

        public static void LogErrorEvent(
           this ILogger logger,
           string serviceId,
           Exception errorException,
           string messageTemplate,
           params object[] propertyValues)
        {
            var props = new object[] { serviceId }.Concat(propertyValues).ToArray();
            logger.LogError(errorException, "{ServiceId:l}:  " + messageTemplate, props);
        }

        public static void LogErrorEvent<T0>(
           this ILogger logger,
           string serviceId,
           Exception errorException,
           string messageTemplate,
           T0 arg0)
        {
            logger.LogError(errorException, "{ServiceId:l}:  " + messageTemplate, serviceId, arg0);
        }

        public static void LogErrorEvent(
           this ILogger logger,
           string serviceId,
           string messageTemplate,
           params object[] propertyValues)
        {
            var props = new object[] { serviceId }.Concat(propertyValues).ToArray();
            logger.LogError("{ServiceId:l}:  " + messageTemplate, props);
        }

        public static void LogErrorEvent<T0>(
           this ILogger logger,
           string serviceId,
           string messageTemplate,
           T0 arg0)
        {
            logger.LogError("{ServiceId:l}:  " + messageTemplate, serviceId, arg0);
        }
    }
}
