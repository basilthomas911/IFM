-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFundOrderTrades] 
	-- Add the parameters for the stored procedure here
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT [FundId] 
	      ,[OrderId]
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
		  ,[UpdatedOn]
		  ,[UpdatedBy]
	FROM [dbo].[fund_order_trade]
	order by [Sequence]

END
