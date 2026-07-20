-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFundOrders] 
	-- Add the parameters for the stored procedure here
	@fundId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT [OrderId]
      ,[FundId]
      ,[OrderDate]
      ,[OrderStatus]
      ,[Reference]
      ,[CreatedOn]
      ,[CreatedBy]
      ,[UpdatedOn]
      ,[UpdatedBy]
	FROM [dbo].[fund_order]
	where FundId = @fundId
	and Deleted = 0
	order by OrderId desc


END
