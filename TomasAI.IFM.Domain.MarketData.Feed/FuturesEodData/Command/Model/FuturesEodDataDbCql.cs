namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;

internal static class FuturesEodDataDbCql
{
	public const string GetLastFuturesEodData = """
		SELECT
			contractId AS "ContractId",
			valueDate AS "ValueDate",
			symbol AS "Symbol",
			openPrice AS "OpenPrice",
			highPrice AS "HighPrice",
			lowPrice AS "LowPrice",
			closePrice AS "ClosePrice",
			volume AS "Volume",
			dailyPercentChange AS "DailyPercentChange",
			dailyStdDev AS "DailyStdDev",
			dailyStdDevAmount AS "DailyStdDevAmount",
			upperBand AS "UpperBand",
			mean AS "Mean",
			lowerBand AS "LowerBand",
			marketDirection AS "MarketDirection",
			marketVolatility AS "MarketVolatility",
			priceDirection AS "PriceDirection",
			priceVolatility AS "PriceVolatility",
			marketDirectionIndicator AS "MarketDirectionIndicator",
			windowSize AS "WindowSize"
		FROM futures_eod_data
		WHERE contractId = :contractId
		AND valueDate < :valueDate
		LIMIT 1;
		""";

	public const string GetFuturesEodDataByDateRange = """
		SELECT
			contractId AS "ContractId",
			valueDate AS "ValueDate",
			symbol AS "Symbol",
			openPrice AS "OpenPrice",
			highPrice AS "HighPrice",
			lowPrice AS "LowPrice",
			closePrice AS "ClosePrice",
			volume AS "Volume",
			dailyPercentChange AS "DailyPercentChange",
			dailyStdDev AS "DailyStdDev",
			dailyStdDevAmount AS "DailyStdDevAmount",
			upperBand AS "UpperBand",
			mean AS "Mean",
			lowerBand AS "LowerBand",
			marketDirection AS "MarketDirection",
			marketVolatility AS "MarketVolatility",
			priceDirection AS "PriceDirection",
			priceVolatility AS "PriceVolatility",
			marketDirectionIndicator AS "MarketDirectionIndicator",
			windowSize AS "WindowSize"
		FROM futures_eod_data
		WHERE contractId = :contractId
		AND valueDate >= :startDate AND valueDate <= :endDate
		ORDER BY valueDate DESC;
		""";

	public const string GetLastVixFuturesEodData = """
		SELECT
			contractId AS "ContractId",
			valueDate AS "ValueDate",
			openPrice AS "OpenPrice",
			highPrice AS "HighPrice",
			lowPrice AS "LowPrice",
			closePrice AS "ClosePrice",
			volume AS "Volume"
		FROM
			vix_futures_eod_data
		WHERE
			contractId = :contractId
			AND valueDate <= :valueDate
		LIMIT 1;
		""";

	public const string GetVixFuturesEodData = """
		SELECT
			contractId AS "ContractId",
			valueDate AS "ValueDate",
			openPrice AS "OpenPrice",
			highPrice AS "HighPrice",
			lowPrice AS "LowPrice",
			closePrice AS "ClosePrice",
			volume AS "Volume"
		FROM
			vix_futures_eod_data
		WHERE
			contractId = :contractId
			AND valueDate = :valueDate
		LIMIT 1;
		""";

	public const string GetNormalCurveData = """
		SELECT
			StdDevIndex AS "StdDevIndex",
			Percent AS "Percent"
		FROM normal_curve_data;
		""";

}
