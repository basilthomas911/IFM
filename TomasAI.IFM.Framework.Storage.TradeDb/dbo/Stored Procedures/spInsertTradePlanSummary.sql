-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertTradePlanSummary] 
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@tradeType varchar(32),
	@tradeDate date,
	@valueDate date,
	@maturityDate date,
	@actionType varchar(32),
	@actionSubType varchar(32),
	@actionState varchar(32),
	@marketTrend varchar(32),
	@marketVolatility varchar(32),
	@actionDate datetime,
	@actionReason varchar(255) = null,
	@tradePnl money,
	@forwardLossRatio real,
	@lossProbability real,
	@mscore real,
	@assetPrice money,
	@assetStdDev real,
	@assetMean real,
	@assetPriceChange real,
	@netPrice money,
	@forwardPrice money,
	@stopLossLimit real,
	@createdOn datetime,
	@createdBy varchar(64)
AS
BEGIN
	
    -- Insert statements for procedure here
	insert into dbo.trade_plan_summary(
				[OrderId],
				[TradeId],
				[TradeType],
				[TradeDate],
				[ValueDate],
				[MaturityDate],
				[ActionType],
				[ActionSubType],
				[ActionState],
				[MarketTrend],
				[MarketVolatility],
				[ActionDate],
				[ActionReason],
				[TradePnl],
				[ForwardLossRatio],
				[LossProbability],
				[MScore],
				[AssetPrice] ,
				[AssetStdDev],
				[AssetMean],
				[AssetPriceChange],
				[NetPrice],
				[ForwardPrice],
				[StopLossLimit],
				[CreatedOn],
				[CreatedBy]
			) values (
				@orderId
			   ,@tradeId
			   ,@tradeType
			   ,@tradeDate
			   ,@valueDate
			   ,@maturityDate
			   ,@actionType
			   ,@actionSubType
			   ,@actionState
			   ,@marketTrend
			   ,@marketVolatility
			   ,@actionDate
			   ,@actionReason
			   ,@tradePnl
			   ,@forwardLossRatio
			   ,@lossProbability
			   ,@mscore
			   ,@assetPrice
			   ,@assetStdDev
			   ,@assetMean
			   ,@assetPriceChange
			   ,@netPrice
			   ,@forwardPrice
			   ,@stopLossLimit
			   ,@createdOn
			   ,@createdBy
  		)
END
