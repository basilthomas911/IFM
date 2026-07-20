-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetOptionTrade]
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
      ,[TradeStrategy]
      ,[TradeDate]
      ,[MaturityDate]
      ,[TradeType]
      ,[TradeState]
      ,[TradeAction]
      ,[UnderlyingContractId]
	  ,[UnderlyingAssetType]
	  ,[IsPrimaryTrade]
	  ,[IsHedgeTrade]
      ,[CreatedOn]
      ,[CreatedBy]
      ,[UpdatedOn]
      ,[UpdatedBy]
	FROM [dbo].[option_trade]
	where OrderId = @orderId
	and TradeId = @tradeId

END