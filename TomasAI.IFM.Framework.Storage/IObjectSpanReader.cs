using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Storage;

internal interface IObjectSpanReader
{
    bool IsNull(ref int start);
    string GetString(ref int start);
    bool GetBool(ref int start);
    bool? GetNullableBool(ref int start);
    int GetInt(ref int start);
    int? GetNullableInt(ref int start);
    short GetShort(ref int start);
    short? GetNullableShort(ref int start);
    long GetLong(ref int start);
    long? GetNullableLong(ref int start);
    double GetDouble(ref int start);
    double? GetNullableDouble(ref int start);
    float GetFloat(ref int start);
    float? GetNullableFloat(ref int start);
    decimal GetDecimal(ref int start);
    decimal? GetNullableDecimal(ref int start);
    DateTime GetDateTime(ref int start);
    DateTime? GetNullableDateTime(ref int start);
    TimeSpan GetTimeSpan(ref int start);
    TimeSpan? GetNullableTimeSpan(ref int start);
    DateOnly GetDateOnly(ref int start);
    DateOnly? GetNullableDateOnly(ref int start);
    TimeOnly GetTimeOnly(ref int start);
    TimeOnly? GetNullableTimeOnly(ref int start);
    Guid GetGuid(ref int start);
    Guid? GetNullableGuid(ref int start);
    TEnum GetEnum<TEnum>(ref int start) where TEnum : struct, Enum;
}
