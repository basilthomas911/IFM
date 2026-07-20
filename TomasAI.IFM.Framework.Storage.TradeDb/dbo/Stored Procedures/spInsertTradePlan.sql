-- =============================================
-- Author:		Basil Thomas
-- Create date: 2018-12-08
-- Description:	insert data into trade plan
-- =============================================
CREATE PROCEDURE [dbo].[spInsertTradePlan] 
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@tradeType varchar(32),
	@tradeDate date,
	@valueDate date,
	@maturityDate date,
	@actionDate datetime,
	@actionType varchar(32),
	@actionSubType varchar(32),
	@actionState varchar(32),
	@actionReason varchar(255) = null,
	@tradePnl money,
	@forwardLossRatio real,
	@lossProbability real,
	@mscore real,
	@maxProfit money,
	@maxLoss money,
	@minProfitTarget money,
	@dailyProfitTarget money,
	@assetPrice money,
	@assetStdDev real,
	@assetMean real,
	@assetPriceChange real,
	@marketTrend varchar(32),
	@marketVolatility varchar(32),
	@marketDirection varchar(32),
	@vixVolatility varchar(32),
	@fiftyDayMA real,
	@twoHundredDayMA real,
	@putSpreadProbability real,
	@callSpreadProbability real,
	@netPrice money,
	@forwardPrice money,
	@stopLossLimit real,
	@createdOn datetime,
	@createdBy varchar(64)

AS
BEGIN

	INSERT INTO dbo.trade_plan
           ([OrderId]
		   ,[TradeId]
		   ,[TradeType]
           ,[TradeDate]
           ,[ValueDate]
           ,[MaturityDate]
           ,[ActionDate]
           ,[ActionType]
           ,[ActionSubType]
		   ,[ActionState]
		   ,[ActionReason]
           ,[TradePnl]
		   ,[ForwardLossRatio]
           ,[LossProbability]
		   ,[MScore]
		   ,[MaxProfit]
		   ,[MaxLoss]
		   ,[MinProfitTarget]
		   ,[DailyProfitTarget]
		   ,[AssetPrice]
		   ,[AssetStdDev]
		   ,[AssetMean]
		   ,[AssetPriceChange]
		   ,[MarketTrend]
		   ,[MarketVolatility]
		   ,[MarketDirection]
		   ,[VixVolatility]
		   ,[FiftyDayMA]
		   ,[TwoHundredDayMA]
		   ,[PutSpreadProbability]
		   ,[CallSpreadProbability]
		   ,[NetPrice]
		   ,[ForwardPrice]
		   ,[StopLossLimit]
           ,[CreatedOn]
		   ,[CreatedBy])
     VALUES
           (@orderId
           ,@tradeId
		   ,@tradeType
           ,@tradeDate
           ,@valueDate
           ,@maturityDate
           ,@actionDate
           ,@actionType
           ,@actionSubType
           ,@actionState
           ,@actionReason
           ,@tradePnl
		   ,@forwardLossRatio
           ,@lossProbability
           ,@mscore
		   ,@maxProfit
		   ,@maxLoss
	       ,@minProfitTarget
	       ,@dailyProfitTarget
	       ,@assetPrice
	       ,@assetStdDev
	       ,@assetMean
		   ,@assetPriceChange
	       ,@marketTrend
	       ,@marketVolatility
	       ,@marketDirection
	       ,@vixVolatility
	       ,@fiftyDayMA
	       ,@twoHundredDayMA
		   ,@putSpreadProbability
		   ,@callSpreadProbability
		   ,@netPrice
		   ,@forwardPrice
		   ,@stopLossLimit
		   ,@createdOn
		   ,@createdBy)

END
