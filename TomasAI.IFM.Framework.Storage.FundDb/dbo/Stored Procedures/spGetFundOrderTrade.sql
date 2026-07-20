-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFundOrderTrade] 
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT [OrderId]
		  ,[TradeId]
		  ,[TradeType]
		  ,[TradeDate]
		  ,[MaturityDate]
		  ,[TradeState]
		  ,[TradeAction]
		  ,[Reference]
		  ,[PrimaryTrade]
		  ,[Sequence]
		  ,[BaseContractSymbol]
		  ,[CreatedOn]
		  ,[CreatedBy]
	FROM [dbo].[fund_order_trade]
	where OrderId = @orderId
	and Orderid = @orderId
	order by [Sequence]

END
