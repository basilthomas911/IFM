-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesTradeSignalLLM] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(32),
	@valueDate date,
	@timestamp datetime,
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
	@priceVolatility real,
	@createdOn datetime=null,
	@createdBy varchar(64)=null
AS
BEGIN

	delete from [dbo].[futures_trade_signal_llm]
	where ContractId = @contractId
	and ValueDate = @valueDate
	and Timestamp = @timeStamp

	INSERT INTO [dbo].[futures_trade_signal_llm]
           ([ContractId]
           ,[ValueDate]
           ,[Timestamp]
           ,[OpenPrice]
           ,[HighPrice]
           ,[LowPrice]
           ,[ClosePrice]
           ,[Volume]
           ,[DailyPercentChange]
           ,[DailyStdDev]
           ,[UpperBand]
           ,[Mean]
           ,[LowerBand]
           ,[PriceVolatility]
		   ,[CreatedOn]
		   ,[CreatedBy])
     VALUES
           (@contractId
           ,@valueDate
           ,@timestamp
           ,@openPrice
           ,@highPrice
           ,@lowPrice
           ,@closePrice
           ,@volume
           ,@dailyPercentChange
           ,@dailyStdDev
           ,@upperBand
           ,@mean
           ,@lowerBand
           ,@priceVolatility
		   ,@createdOn
		   ,@createdBy)

END
