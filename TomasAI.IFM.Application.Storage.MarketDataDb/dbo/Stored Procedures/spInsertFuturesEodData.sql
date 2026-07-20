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
	@dailyStdDevAmount real,
	@upperBand real,
	@mean real,
	@lowerBand real,
	@marketDirection varchar(32),
	@marketVolatility varchar(32),
	@priceDirection varchar(32),
	@priceVolatility varchar(32),
	@marketDirectionIndicator real,
	@windowSize int
AS
BEGIN

	if (exists(select * from dbo.futures_eod_data where ContractId = @contractId and ValueDate = @valueDate))
		update dbo.futures_eod_data
		set 
			--OpenPrice = (select td.Price from futures_tick_data td where td.ContractId = @contractId and Valuedate = @valueDate and td.TickTime = (
			--	select min(ftd.TickTime) from futures_tick_data ftd where ftd.ContractId = td.ContractId and ftd.ValueDate = td.ValueDate
			--)),
			OpenPrice = (select top 1 ClosingPrice from futures_closing_price where ContractId = @contractId and ValueDate < @valueDate	order by ValueDate desc),
			HighPrice = (select max(td.Price) from futures_tick_data td where td.ContractId = @contractId and td.ValueDate = @valueDate),
			LowPrice = (select min(td.Price) from futures_tick_data td where td.ContractId = @contractId and td.ValueDate = @valueDate),
			ClosePrice = @closePrice,
			Volume = (select sum(td.Size) from futures_tick_data td where td.ContractId = @contractId and td.ValueDate = @valueDate),
			DailyPercentChange = @dailyPercentChange,
			DailyStdDev = @dailyStdDev,
			DailyStdDevAmount = @dailyStdDevAmount,
			UpperBand = @upperBand,
			Mean = @mean,
			LowerBand = @lowerBand,
			MarketDirection = @marketDirection,
			MarketVolatility = @marketVolatility,
			PriceDirection = @priceDirection,
			PriceVolatility = @priceVolatility,
			MarketDirectionIndicator = @marketDirectionIndicator,
			WindowSize = @windowSize
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
			DailyStdDevAmount,
			UpperBand,
			Mean,
			LowerBand,
			MarketDirection,
			MarketVolatility,
			PriceDirection,
			PriceVolatility,
			MarketDirectionIndicator,
			WindowSize
		) values (
			@contractId,
			@valueDate,
			(select top 1 ClosingPrice from futures_closing_price where ContractId = @contractId and ValueDate <= @valueDate	order by ValueDate desc),
			@highPrice,
			@lowPrice,
			@closePrice,
			@volume,
			@dailyPercentChange,
			@dailyStdDev,
			@dailyStdDevAmount,
			@upperBand,
			@mean,
			@lowerBand,
			@marketDirection,
			@marketVolatility,
			@priceDirection,
			@priceVolatility,
			@marketDirectionIndicator,
			@windowSize
		)
	
END
