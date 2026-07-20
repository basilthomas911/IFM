-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesItiTrendData] 
	@symbol varchar(10),
	@valueDate date,
	@timestamp datetime,
	@trendDelta real,
	@targetDelta real,
	@trendDirection int,
	@trendDirectionMode int,
	@futuresPrice real,
	@trendExtreme real,
	@futuresMDI real,
	@futuresStdDev real,
	@futuresRSI real,
	@futuresFiftyDMA real,
	@futuresTwoHundredDMA real
AS
BEGIN


	INSERT INTO [dbo].[futures_iti_trend_data]
           ([Symbol]
           ,[ValueDate]
           ,[Timestamp]
           ,[TrendDelta]
		   ,[TargetDelta]
           ,[TrendDirection]
           ,[TrendDirectionMode]
           ,[FuturesPrice]
           ,[TrendExtreme]
           ,[FuturesMDI]
           ,[FuturesStdDev]
           ,[FuturesRSI]
           ,[FuturesFiftyDMA]
           ,[FuturesTwoHundredDMA])
     VALUES
           (@symbol
           ,@valueDate
           ,@timestamp
           ,@trendDelta
		   ,@targetDelta
		   ,@trendDirection
		   ,@trendDirectionMode
           ,@futuresPrice
           ,@trendExtreme
           ,@futuresMDI
		   ,@futuresStdDev
		   ,@futuresRSI
		   ,@futuresFiftyDMA
		   ,@futuresTwoHundredDMA)

END
