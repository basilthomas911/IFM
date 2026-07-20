-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertTradeTypeLimit] 

	-- Add the parameters for the stored procedure here
	@tradeId int,
	@tradeType varchar(32),
	@maxLossLimit real,
	@minProfitLimit real
AS
BEGIN

	if not exists(select * from dbo.trade_type_limit where TradeId = @tradeId and TradeType = @tradeType)
		insert into dbo.trade_type_limit(
			TradeId,
			TradeType,
			MaxLossLimit,
			MinProfitLimit
		) values (
			@tradeId,
			@tradeType,
			@maxLossLimit,
			@minProfitLimit)
	else
		update dbo.trade_type_limit
		set MaxLossLimit = @maxLossLimit,
			MinProfitLimit = @minProfitLimit
		where TradeId = @tradeId
		and TradeType = @tradeType

END
