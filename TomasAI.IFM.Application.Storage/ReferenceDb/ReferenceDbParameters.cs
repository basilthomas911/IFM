using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using Npgsql;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
internal readonly record struct ReferenceParameters(NpgsqlParameter[] Values) : IBindValue
{
    public object Bind() => Values;
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteMDIForwardLossRatio(string trendDirection, string tradeType) : IBindValue
{
    public object Bind() => new NpgsqlParameter[]
    {
        Text(trendDirection),
        Text(tradeType)
    };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLookupType(string lookupTypeName) : IBindValue
{
    public object Bind() => Values(Text(lookupTypeName));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetLookupTypeById(string lookupTypeName, int orderId) : IBindValue
{
    public object Bind() => Values(Text(lookupTypeName), Integer(orderId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMDIForwardLossRatios(string trendDirection, string tradeType) : IBindValue
{
    public object Bind() => new NpgsqlParameter[]
    {
        Text(trendDirection),
        Text(tradeType)
    };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetTradeStrategyFamilies(string catalog) : IBindValue
{
    public object Bind() => Values(Text(catalog));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertMDIForwardLossRatio(int mdi, string trendDirection, string tradeType, double forwardLossRatio, string createdBy, DateTime? createdOn, string updatedBy, DateTime? updatedOn) : IBindValue
{
    public object Bind() => new NpgsqlParameter[]
    {
        Integer(mdi),
        Text(trendDirection),
        Text(tradeType),
        Double(forwardLossRatio),
        Text(createdBy),
        TimestampTz(createdOn),
        Text(updatedBy),
        TimestampTz(updatedOn)
    };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradeStrategyFamily(string catalog, int tradeStrategyFamilyId, long definitionVersion, string systemKey, string family, string strategy, string timeFrame, string symbol, string currency, string description, string state, DateTime createdOnUtc, string createdBy) : IBindValue
{
    public object Bind() => Values(Text(catalog), Integer(tradeStrategyFamilyId), Bigint(definitionVersion), Text(systemKey), Text(family), Text(strategy), Text(timeFrame), Text(symbol), Text(currency), Text(description), Text(state), TimestampTz(createdOnUtc), Text(createdBy));
}
