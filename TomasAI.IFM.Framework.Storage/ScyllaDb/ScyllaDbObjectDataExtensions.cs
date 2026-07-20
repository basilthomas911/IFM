using Cassandra;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.Commands;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb
{
    public static class ScyllaDbObjectDataExtensions
    {
        public static List<TResult>? Get<TResult>(this IObjectMapReader<TResult> objectMapReader,  Expression<Func<TResult, List<TResult>>> resultPropertyExpr)
            => GetReader(objectMapReader).Get(resultPropertyExpr);

        public static Dictionary<TKey, TResult>? Get<TKey, TResult>(this IObjectMapReader<TResult> objectMapReader, Expression<Func<TResult, Dictionary<TKey, TResult>>> resultPropertyExpr) where TKey : notnull
            => GetReader(objectMapReader).Get(resultPropertyExpr);

        public static HashSet<TResult>? Get<TResult>(this IObjectMapReader<TResult> objectMapReader, Expression<Func<TResult, HashSet<TResult>>> resultPropertyExpr)
            => GetReader(objectMapReader).Get(resultPropertyExpr);

        public static CqlVector<TResult>? Get<TResult>(this IObjectMapReader<TResult> objectMapReader, Expression<Func<TResult, CqlVector<TResult>>> resultPropertyExpr)
            => GetReader(objectMapReader).Get(resultPropertyExpr);

        public static DateTime AsDateTime(this DateOnly dateOnly)
            => dateOnly.ToDateTime(TimeOnly.MinValue);

        public static DateOnly AsDateOnly(this DateTime dateTime)
            => new DateOnly(dateTime.Year, dateTime.Month, dateTime.Day);

        public static LocalDate AsLocalDate(this DateTime dateTime)
            => new LocalDate(dateTime.Year, dateTime.Month, dateTime.Day);

        public static LocalTime AsLocalTime(this DateTime dateTime)
            => new LocalTime(dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Nanosecond);

        public static int AsMilliseconds(this LocalTime localTime)
            => (int)(localTime.TotalNanoseconds % 1000000000) % 1000000;

        public static int AsMicroseconds(this LocalTime localTime)
            => (int)(localTime.TotalNanoseconds % 1000000000) % 1000;

        static IScyllaDbObjectMapReader<TResult> GetReader<TResult>(IObjectMapReader<TResult> objectMapReader)
        {
            var mapReader = objectMapReader as IScyllaDbObjectMapReader<TResult>;
            if (mapReader is null)
                throw new InvalidOperationException($"ScyllaDbExtensions.Get: map reader is not of type IScyllaDbObjectMapReader");
            return mapReader;
        }
    }
}
