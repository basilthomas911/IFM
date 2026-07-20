-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertTradePosition]
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@tradeType varchar(32),
	@valueDate datetime,
	@daysToExpiry int,
	@tradeStatus varchar(32),
	@commission real=null,
	@deltaHedge int,
	@netSpread real,
	@tradeValue real,
	@tradePnl real = null,
	@assetPrice real,
	@otmProbability real,
	@forwardPrice money,
	@forwardLossRatio real = null,
	@lossProbability real=null,
	@riskFreeRate real,
	@createdOn datetime=null,
	@createdBy varchar(64)=null,
	@updatedOn datetime=null,
	@updatedBy varchar(64)=null
AS
BEGIN
	if exists(select * from dbo.trade_position
					where OrderId = @orderId
					and TradeId = @tradeId 
					and TradeType = @tradeType 
					and ValueDate = @valueDate 
					and DaysToExpiry = @daysToExpiry 
					and TradeStatus = @tradeStatus)
		update dbo.trade_position
		set Commission = @commission,
			DeltaHedge = @deltaHedge,
			NetSpread = @netSpread,
			TradeValue = @tradeValue,
			TradePnl = @tradePnl,
			AssetPrice = @assetPrice,
			OTMProbability = @otmProbability,
			ForwardPrice = @forwardPrice,
			ForwardLossRatio = @forwardLossRatio,
			LossProbability = @lossProbability,
			RiskFreeRate = @riskFreeRate,
			UpdatedOn = @updatedOn,
			UpdatedBy = @updatedBy
		where Orderid = @orderId
		and Tradeid = @tradeId
		and TradeType = @tradeType
		and ValueDate = @valueDate
		and DaysToExpiry = @daysToExpiry
		and TradeStatus = @tradeStatus
	else
		insert into dbo.trade_position (
			OrderId,
			TradeId,
			TradeType,
			ValueDate,
			DaysToExpiry,
			TradeStatus,
			Commission,
			DeltaHedge,
			NetSpread,
			TradeValue,
			TradePnl,
			AssetPrice,
			OTMProbability,
			ForwardPrice,
			ForwardLossRatio,
			LossProbability,
			RiskFreeRate,
			CreatedOn,
			CreatedBy,
			UpdatedOn,
			UpdatedBy
		) values (
			@orderId,
			@tradeId,
			@tradeType, 
			@valueDate,
			@daysToExpiry, 
			@tradeStatus,
			@commission,
			@deltaHedge,
			@netSpread,
			@tradeValue,
			@tradePnl,
			@assetPrice,
			@otmProbability,
			@forwardPrice,
			@forwardLossRatio,
			@lossProbability,
			@riskFreeRate,
			@createdOn,
			@createdBy,
			@updatedOn,
			@updatedBy)

END
