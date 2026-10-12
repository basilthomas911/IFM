using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetTradeStrategyFamilies(string catalog) : IBindValue { public bool PostgreSql { get; init; } public object Bind() => PostgreSql ? Values(Text(catalog)) : new object?[] { catalog }; }
