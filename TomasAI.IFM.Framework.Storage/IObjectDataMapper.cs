using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage;

public interface IObjectDataMapper<TResult> where TResult : class
{
    TResult As();

    string GetString(string columnName);
    bool GetBool(string columnName);
    bool? GetNullableBool(string columnName);
    int GetInt(string columnName);
    int? GetNullableInt(string columnName);
    short GetShort(string columnName);
    short? GetNullableShort(string columnName);
    long GetLong(string columnName);
    long? GetNullableLong(string columnName);
    double GetDouble(string columnName);
    double? GetNullableDouble(string columnName);
    float GetFloat(string columnName);
    float? GetNullableFloat(string columnName);
    decimal GetDecimal(string columnName);
    decimal? GetNullableDecimal(string columnName);
    DateTime GetDateTime(string columnName);
    DateTime? GetNullableDateTime(string columnName);
    TimeSpan GetTimeSpan(string columnName);
    TimeSpan? GetNullableTimeSpan(string columnName);
    DateOnly GetDateOnly(string columnName);
    DateOnly? GetNullableDateOnly(string columnName);
    TimeOnly GetTimeOnly(string columnName);
    TimeOnly? GetNullableTimeOnly(string columnName);
    Guid GetGuid(string columnName);
    Guid? GetNullableGuid(string columnName);
    byte[] GetBytes(string columnName);
    TEnum GetEnum<TEnum>(string columnName) where TEnum : struct, Enum;
    TStruct GetStruct<TStruct>(string columnName) where TStruct : struct;
    DateTime GetISODateTime(string columnName);
}
