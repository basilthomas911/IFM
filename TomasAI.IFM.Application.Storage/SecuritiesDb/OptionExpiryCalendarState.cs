using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal sealed record OptionExpiryCalendarState(
    Guid Generation,
    DateOnly CoverageFrom,
    DateOnly CoverageThrough,
    DateTime RefreshedAtUtc);
