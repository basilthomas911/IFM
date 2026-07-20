namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;

internal static class FuturesEodDataDbCql
{
	public const string GetFuturesEodClosingPrices = """
		SELECT
			symbol AS "Symbol",
			valueDate AS "ValueDate",
			closePrice AS "ClosingPrice"
		FROM futures_eod_data
		WHERE contractId = :contractId
		and valueDate >= :startDate
		AND valueDate <= :endDate
		AND symbol = :symbol
		ORDER BY valueDate DESC
		LIMIT :maxDays
		ALLOW FILTERING;
		""";

	public const string GetFuturesEodData = """
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
		AND valueDate = :valueDate
		LIMIT 1;
		""";

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

	public const string GetYesterdaysFuturesEodData = """
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
		WHERE valueDate < :valueDate
		LIMIT 1
		ALLOW FILTERING;
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

	public const string GetVixFuturesEodDataByValueDate = """
		SELECT
			ContractId AS "ContractId",
			ValueDate AS "ValueDate",
			OpenPrice AS "OpenPrice",
			HighPrice AS "HighPrice",
			LowPrice AS "LowPrice",
			ClosePrice AS "ClosePrice",
			Volume AS "Volume"
		FROM vix_futures_eod_data
		WHERE ValueDate <= :valueDate
		ORDER BY ValueDate DESC ALLOW FILTERING;
		""";

	public const string GetNormalCurveData = """
		SELECT
			StdDevIndex AS "StdDevIndex",
			Percent AS "Percent"
		FROM normal_curve_data;
		""";

	public const string InsertFuturesEodData = """
		INSERT INTO futures_eod_data (
			contractId,
			valueDate,
			symbol,
			openPrice,
			highPrice,
			lowPrice,
			closePrice,
			volume,
			dailyPercentChange,
			dailyStdDev,
			dailyStdDevAmount,
			upperBand,
			mean,
			lowerBand,
			marketDirection,
			marketVolatility,
			priceDirection,
			priceVolatility,
			marketDirectionIndicator,
			windowSize
		) VALUES (
			:contractId,
			:valueDate,
			:symbol,
			:openPrice,
			:highPrice,
			:lowPrice,
			:closePrice,
			:volume,
			:dailyPercentChange,
			:dailyStdDev,
			:dailyStdDevAmount,
			:upperBand,
			:mean,
			:lowerBand,
			:marketDirection,
			:marketVolatility,
			:priceDirection,
			:priceVolatility,
			:marketDirectionIndicator,
			:windowSize
		);
		""";

	public const string InsertVixFuturesEodData = """
		INSERT INTO vix_futures_eod_data (contractId, valueDate, openPrice, highPrice, lowPrice, closePrice, volume)
		VALUES (:contractId, :valueDate, :price, :price, :price, :price, :size);
		""";
}
