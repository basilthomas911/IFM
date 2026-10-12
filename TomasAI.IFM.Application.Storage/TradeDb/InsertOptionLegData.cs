using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertOptionLegData(int orderId, int tradeId, string tradeType, DateOnly valueDate, int daysToExpiry, string tradeStatus, string optionLegId, decimal bidPrice, decimal askPrice, double impliedVolatility, double delta, double gamma, double theta, double vega, double rho, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus, optionLegId, bidPrice, askPrice, impliedVolatility, delta, gamma, theta, vega, rho, createdOn, createdBy, updatedOn, updatedBy };
}
