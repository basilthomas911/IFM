using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateOptionLegData(int orderId, int tradeId, DateOnly valueDate, string optionLegId, decimal bidPrice, decimal askPrice, double impliedVolatility, double delta, double gamma, double theta, double vega, double rho, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { bidPrice, askPrice, impliedVolatility, delta, gamma, theta, vega, rho, updatedOn, updatedBy, orderId, tradeId, valueDate, optionLegId };
}
