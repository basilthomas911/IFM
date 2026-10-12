using Npgsql;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertYieldCurveRate(int id, DateOnly valueDate, double oneMonth, double twoMonth, double threeMonth, double sixMonth, double oneYear, double twoYear, double threeYear, double fiveYear, double sevenYear, double tenYear, double twentyYear, double thirtyYear) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? new NpgsqlParameter[]
    {
        Integer(id),
        Date(valueDate),
        Double(oneMonth),
        Double(twoMonth),
        Double(threeMonth),
        Double(sixMonth),
        Double(oneYear),
        Double(twoYear),
        Double(threeYear),
        Double(fiveYear),
        Double(sevenYear),
        Double(tenYear),
        Double(twentyYear),
        Double(thirtyYear)
    } : new object?[] { id, valueDate, oneMonth, twoMonth, threeMonth, sixMonth, oneYear, twoYear, threeYear, fiveYear, sevenYear, tenYear, twentyYear, thirtyYear };
}
