using Npgsql;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct OptionVolatilityParameters(object?[] Items) : IBindValue
{
    public bool PostgreSql { get; init; }
    public object Bind() => PostgreSql ? Items.Select(ToParameter).ToArray() : Items;
    static NpgsqlParameter ToParameter(object? value) => value switch
    {
        string text => Text(text), int number => Integer(number), long number => Bigint(number),
        DateOnly date => Date(date), DateTime timestamp => TimestampTz(timestamp), byte[] payload => Bytea(payload),
        _ => throw new ArgumentException("Unsupported volatility parameter type.",nameof(value))
    };
}
