-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFundOrderTrade] 
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@tradeType varchar(32),
	@tradeDate date,
	@maturityDate date,
	@tradeState varchar(32),
	@tradeAction varchar(32),
	@reference varchar(255),
	@primaryTrade bit,
	@baseContractSymbol char(5),
	@createdOn datetime,
	@createdBy varchar(64)
AS
BEGIN
	
	if not exists(select * from dbo.fund_order_trade where Orderid = @orderid and TradeId = @tradeId)
		insert into dbo.fund_order_trade(
			OrderId,
			TradeId,
			TradeType,
			TradeDate,
			MaturityDate,
			TradeState,
			TradeAction,
			Reference,
			PrimaryTrade,
			BaseContractSymbol,
			CreatedOn,
			CreatedBy
		) values (
			@orderId,
			@tradeId,
			@tradeType,
			@tradeDate,
			@maturityDate,
			@tradeState,
			@tradeAction,
			@reference,
			@primaryTrade,
			@baseContractSymbol,
			@createdOn,
			@createdBy
		)
END
