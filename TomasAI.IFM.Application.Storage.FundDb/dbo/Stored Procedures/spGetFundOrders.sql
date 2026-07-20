-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFundOrders] 
	-- Add the parameters for the stored procedure here
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT [OrderId]
      ,[FundId]
      ,[OrderDate]
      ,[OrderStatus]
	  ,[BaseContractId]
	  ,[TradeDate]
	  ,[MaturityDate]
      ,[Reference]
      ,[CreatedOn]
      ,[CreatedBy]
      ,[UpdatedOn]
      ,[UpdatedBy]
	FROM [dbo].[fund_order]
	where Deleted = 0
	order by OrderId desc

END
