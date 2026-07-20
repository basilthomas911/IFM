-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateTradePosition]
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@tradeType varchar(32),
	@valueDate date,
	@daysToExpiry int,
	@tradeStatus varchar(32),
	@commission money,
	@deltaHedge int,
	@netSpread money,
	@tradeValue money,
	@tradePnl money,
	@assetPrice money,
	@otmProbability real,
	@forwardPrice money,
	@lossProbability real,
	@riskFreeRate real,
	@updatedOn datetime,
	@updatedBy varchar(64)

AS
BEGIN

    -- Insert statements for procedure here
	update dbo.trade_position
	set Commission = @commission,
		DeltaHedge = @deltaHedge,
		NetSpread = @netSpread,
		TradeValue = @tradeValue,
		TradePnl = @tradePnl,
		AssetPrice = @assetPrice,
		OTMProbability = @otmProbability,
		ForwardPrice = @forwardPrice,
		LossProbability = @lossProbability,
		RiskFreeRate = @riskFreeRate,
		UpdatedOn = @updatedOn,
		UpdatedBy = @updatedBy
	where OrderId = @orderId 
	and Tradeid = @tradeId
	and TradeType = @tradeType
	and ValueDate = @valueDate
	and DaysToExpiry = @daysToExpiry
	and TradeStatus = @tradeStatus
END
