using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct VxPairParameter(
    FuturesRolloverContractAssignment Front,
    long FrontExpected,
    FuturesRolloverContractAssignment Second,
    long SecondExpected) : IBindValue
{
    public object Bind() => Values([.. Front.BindAssignment(FrontExpected), .. Second.BindAssignment(SecondExpected)]);
}
