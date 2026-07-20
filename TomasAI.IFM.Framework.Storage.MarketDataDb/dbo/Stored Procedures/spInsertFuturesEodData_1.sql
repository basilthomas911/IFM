-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesEodData]
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date,
	@openPrice real,
	@highPrice real,
	@lowPrice real,
	@closePrice real,
	@volume int,
	@dailyPercentChange real,
	@dailyStdDev real,
	@upperBand real,
	@mean real,
	@lowerBand real,
	@marketTrend varchar(32),
	@marketVolatility varchar(32),
	@marketDirection varchar(32),
	@vixVolatility varchar(32),
	@fiftyDayMA real,
	@FiveDayXMA real
AS
BEGIN
/* 
isnull( (select eod.ClosePrice from futures_eod_data eod where eod.ContractId = @contractId and eod.ValueDate = (
				select max(ValueDate) from futures_eod_data eod where eod.ContractId = @contractId and eod.ValueDate < @valueDate
			)), @openPrice),
*/

	if (exists(select * from dbo.futures_eod_data where ContractId = @contractId and ValueDate = @valueDate))
		update dbo.futures_eod_data
		set 
			OpenPrice = (select td.Price from futures_tick_data td where td.ContractId = @contractId and Valuedate = @valueDate and td.TickTime = (
				select min(ftd.TickTime) from futures_tick_data ftd where ftd.ContractId = td.ContractId and ftd.ValueDate = td.ValueDate
			)),
			HighPrice = (select max(td.Price) from futures_tick_data td where td.ContractId = @contractId and td.ValueDate = @valueDate),
			LowPrice = (select min(td.Price) from futures_tick_data td where td.ContractId = @contractId and td.ValueDate = @valueDate),
			ClosePrice = @closePrice,
			Volume = (select sum(td.Size) from futures_tick_data td where td.ContractId = @contractId and td.ValueDate = @valueDate),
			DailyPercentChange = @dailyPercentChange,
			DailyStdDev = @dailyStdDev,
			UpperBand = @upperBand,
			Mean = @mean,
			LowerBand = @lowerBand,
			MarketTrend = @marketTrend,
			MarketVolatility = @marketVolatility,
			MarketDirection = @marketDirection,
			VixVolatility = @vixVolatility,
			FiftyDayMA = @fiftyDayMA,
			FiveDayXMA = @FiveDayXMA
		where ContractId = @contractId and ValueDate = @valueDate
	else
		insert into dbo.futures_eod_data (
			ContractId,
			ValueDate,
			OpenPrice,
			HighPrice ,
			LowPrice,
			ClosePrice,
			Volume,
			DailyPercentChange,
			DailyStdDev,
			UpperBand,
			Mean,
			LowerBand,
			MarketTrend,
			MarketVolatility,
			MarketDirection,
			VixVolatility,
			FiftyDayMA,
			FiveDayXMA
		) values (
			@contractId,
			@valueDate,
			@openPrice,
			@highPrice,
			@lowPrice,
			@closePrice,
			@volume,
			@dailyPercentChange,
			@dailyStdDev,
			@upperBand,
			@mean,
			@lowerBand,
			@marketTrend,
			@marketVolatility,
			@marketDirection,
			@vixVolatility,
			@fiftyDayMA,
			@FiveDayXMA
		)
	
END
