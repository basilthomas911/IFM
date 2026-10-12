using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetNativeTradeOrder(int PortfolioId,int FundId,int OrderId) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql
        ? TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter.Values(
            TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter.Integer(PortfolioId),
            TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter.Integer(FundId),
            TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter.Integer(OrderId))
        : new object?[] {PortfolioId,FundId,OrderId};
}
