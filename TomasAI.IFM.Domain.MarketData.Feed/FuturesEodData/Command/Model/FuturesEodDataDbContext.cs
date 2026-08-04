using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;

internal static class FuturesEodDataDbContext
{
	static NormalCurveTableReadModel? _normalCurveTable;

	/// <summary>
	/// Retrieves end-of-day futures data for a contract and value date.
	/// Falls back to the most recent prior value date when no exact match exists.
	/// </summary>
	internal static async ValueTask<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(
		this IDbContextFactory dbFactory, string contractId, DateOnly valueDate)
		=> await dbFactory.MarketDataDb.GetFuturesEodDataAsync(contractId, valueDate);

	/// <summary>
	/// Retrieves the most recent end-of-day futures data before the supplied value date.
	/// </summary>
	internal static async ValueTask<FuturesEodDataV2ReadModel?> GetLastFuturesEodDataAsync(
		this IDbContextFactory dbFactory, string contractId, DateOnly valueDate)
		=> await dbFactory.MarketDataDb
			.Use(FuturesEodDataDbCql.GetLastFuturesEodData)
			.SetParameters(new GetLastFuturesEodData(contractId, valueDate))
			.ExecuteSingleAsync(MapToFuturesEodData);

	/// <summary>
	/// Retrieves end-of-day futures data records for a contract within a date range.
	/// </summary>
	internal static async ValueTask<FuturesEodDataV2ReadModel[]> GetFuturesEodDataByDateRangeAsync(
		this IDbContextFactory dbFactory, string contractId, DateOnly startDate, DateOnly endDate)
		=> [.. await dbFactory.MarketDataDb
			.Use(FuturesEodDataDbCql.GetFuturesEodDataByDateRange)
			.SetParameters(new GetFuturesEodDataByDateRange(contractId, startDate, endDate))
			.ExecuteQueryAsync(MapToFuturesEodData)];

	/// <summary>
	/// Retrieves close prices for moving-average calculations.
	/// </summary>
	internal static async ValueTask<FuturesEodClosingPriceReadModel[]> GetFuturesEodClosingPricesAsync(
		this IDbContextFactory dbFactory, string contractId, string symbol, DateOnly startDate, DateOnly endDate, int maxDays)
		=> [.. await dbFactory.MarketDataDb.GetFuturesEodClosingPricesAsync(
			contractId, symbol, startDate, endDate, maxDays)];

	/// <summary>
	/// Retrieves the most recent VIX futures EOD record up to the supplied value date.
	/// </summary>
	internal static async ValueTask<VixFuturesEodDataReadModel?> GetLastVixFuturesEodDataAsync(
		this IDbContextFactory dbFactory, string contractId, DateOnly valueDate)
		=> await dbFactory.MarketDataDb
			.Use(FuturesEodDataDbCql.GetLastVixFuturesEodData)
			.SetParameters(new GetLastVixFuturesEodData(contractId, valueDate))
			.ExecuteSingleAsync(MapToVixFuturesEodData);

	/// <summary>
	/// Retrieves VIX futures EOD for a contract/value-date pair.
	/// </summary>
	internal static async ValueTask<VixFuturesEodDataReadModel?> GetVixFuturesEodDataAsync(
		this IDbContextFactory dbFactory, string contractId, DateOnly valueDate)
		=> await dbFactory.MarketDataDb
			.Use(FuturesEodDataDbCql.GetVixFuturesEodData)
			.SetParameters(new GetVixFuturesEodData(contractId, valueDate))
			.ExecuteSingleAsync(MapToVixFuturesEodData);

	/// <summary>
	/// Retrieves VIX futures EOD data for all contracts up to the supplied value date.
	/// </summary>
	internal static async ValueTask<VixFuturesEodDataReadModel[]> GetVixFuturesEodDataByValueDateAsync(
		this IDbContextFactory dbFactory, DateOnly valueDate)
		=> [.. await dbFactory.MarketDataDb.GetVixFuturesEodDataByValueDateAsync(valueDate)];

	/// <summary>
	/// Retrieves the normal curve table used by futures EOD calculations.
	/// </summary>
	internal static async ValueTask<NormalCurveTableReadModel> GetNormalCurveTableAsync(this IDbContextFactory dbFactory)
	{
		_normalCurveTable ??= new NormalCurveTableReadModel([.. await dbFactory.MarketDataDb
			.Use(FuturesEodDataDbCql.GetNormalCurveData)
			.ExecuteQueryAsync(MapToNormalCurveData)]);
		return _normalCurveTable;
	}

	

	/// <summary>
	/// Retrieves the composite parameters object required for EOD futures data generation.
	/// </summary>
	internal static async ValueTask<FuturesEodDataParametersReadModel> GetFuturesEodDataParametersAsync(
		this IDbContextFactory dbFactory, string contractId, DateOnly valueDate)
		=> new FuturesEodDataParametersReadModel(
			FuturesEodDataToday: (await dbFactory.GetFuturesEodDataAsync(contractId, valueDate))!,
			FuturesEodDataRange: await dbFactory.GetFuturesEodDataByDateRangeAsync(contractId, valueDate.AddMonths(-2), valueDate.AddDays(-1)),
			NormalCurveTable: await dbFactory.GetNormalCurveTableAsync());

	/// <summary>
	/// Inserts a futures EOD data record into the database.
	/// </summary>
	/// <param name="dbFactory">The database context factory.</param>
	/// <param name="e">The futures EOD data read model to persist.</param>
	internal static async ValueTask InsertFuturesEodDataAsync(
		this IDbContextFactory dbFactory, FuturesEodDataV2ReadModel e)
		=> await dbFactory.MarketDataDb.InsertFuturesEodDataAsync(e);

	/// <summary>
	/// Inserts a VIX futures EOD data record into the database.
	/// </summary>
	/// <param name="dbFactory">The database context factory.</param>
	/// <param name="e">The VIX futures tick data read model to persist.</param>
	internal static async ValueTask InsertVixFuturesEodDataAsync(
		this IDbContextFactory dbFactory, FuturesTickDataV2ReadModel e)
		=> await dbFactory.MarketDataDb.InsertVixFuturesEodDataAsync(e);

	static FuturesEodDataV2ReadModel MapToFuturesEodData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
		=> new(
			contractId: e.GetString(0),
			valueDate: e.GetDateOnly(1),
			symbol: e.GetString(2),
			openPrice: e.GetDecimal(3),
			highPrice: e.GetDecimal(4),
			lowPrice: e.GetDecimal(5),
			closePrice: e.GetDecimal(6),
			volume: e.GetInt(7),
			dailyPercentChange: e.GetDouble(8),
			dailyStdDev: e.GetDouble(9),
			dailyStdDevAmount: e.GetDouble(10),
			upperBand: e.GetDouble(11),
			mean: e.GetDouble(12),
			lowerBand: e.GetDouble(13),
			marketDirection: e.GetEnum<MarketDirectionType>(14),
			marketVolatility: e.GetEnum<MarketVolatilityType>(15),
			priceDirection: e.GetEnum<PriceDirectionType>(16),
			priceVolatility: e.GetEnum<PriceVolatilityType>(17),
			marketDirectionIndicator: e.GetDouble(18),
			windowSize: e.GetInt(19)
		);

	static FuturesEodClosingPriceReadModel MapToFuturesEodClosingPrice<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
		=> new(
			symbol: e.GetString(0),
			valueDate: e.GetDateOnly(1),
			closingPrice: e.GetDecimal(2)
		);

	static VixFuturesEodDataReadModel MapToVixFuturesEodData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
		=> new(
			contractId: e.GetString(0),
			valueDate: e.GetDateOnly(1),
			openPrice: e.GetDecimal(2),
			highPrice: e.GetDecimal(3),
			lowPrice: e.GetDecimal(4),
			closePrice: e.GetDecimal(5),
			volume: e.GetInt(6)
		);

	static NormalCurveDataReadModel MapToNormalCurveData<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
		=> new(
			stdDevIndex: e.GetInt(0),
			percent: e.GetDouble(1)
		);

	internal readonly record struct GetLastFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
	{
		public object Bind() => new object?[] { contractId, valueDate };
	}

	internal readonly record struct GetFuturesEodDataByDateRange(string contractId, DateOnly startDate, DateOnly endDate) : IBindValue
	{
		public object Bind() => new object?[] { contractId, startDate, endDate };
	}

	internal readonly record struct GetLastVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
	{
		public object Bind() => new object?[] { contractId, valueDate };
	}

	internal readonly record struct GetVixFuturesEodData(string contractId, DateOnly valueDate) : IBindValue
	{
		public object Bind() => new object?[] { contractId, valueDate };
	}

}
