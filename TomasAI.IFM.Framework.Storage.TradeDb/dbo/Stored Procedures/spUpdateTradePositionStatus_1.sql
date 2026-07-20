-- =============================================
-- Author:		Basil Thomas
-- Create date: 2018-07-07
-- Description:	update trade status
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateTradePositionStatus] 
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@tradeType varchar(64),
	@valueDate datetime,
	@daysToExpiry int,
	@oldTradeStatus varchar(32),
	@newTradeStatus varchar(32),
	@updatedOn datetime,
	@updatedBy varchar(64)
AS
BEGIN
	update dbo.trade_position
	set TradeStatus = @newTradeStatus,
		UpdatedOn = @updatedOn,
		UpdatedBy = @updatedBy
	where TradeId = @tradeId
	and TradeType = @tradeType
	and ValueDate = @valueDate
	and DaysToExpiry = @daysToExpiry
	and TradeStatus = @oldTradeStatus

	update dbo.option_leg_data
	set TradeStatus = @newTradeStatus,
		UpdatedOn = @updatedOn,
		UpdatedBy = @updatedBy
	where TradeId = @tradeId
	and TradeType = @tradeType
	and ValueDate = @valueDate
	and DaysToExpiry = @daysToExpiry
	and TradeStatus = @oldTradeStatus

END
