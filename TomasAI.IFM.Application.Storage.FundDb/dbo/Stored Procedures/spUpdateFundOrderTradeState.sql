-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateFundOrderTradeState] 
	-- Add the parameters for the stored procedure here
	@fundId int,
	@orderId int,
	@tradeId int,
	@tradeState varchar(32), 
	@updatedOn datetime,
	@updatedBy varchar(64)

AS
BEGIN
	
	update dbo.fund_order_trade
	set TradeState = @tradeState,
		UpdatedOn = @updatedOn,
		UpdatedBy = @updatedBy
	where FundId = @fundId 
	and OrderId = @orderId
	and TradeId = @tradeId
END
