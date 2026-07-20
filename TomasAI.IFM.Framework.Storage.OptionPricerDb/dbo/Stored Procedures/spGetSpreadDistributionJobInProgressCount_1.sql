-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetSpreadDistributionJobInProgressCount] 
	@orderId  int,
	@tradeId int
AS
BEGIN
	SET NOCOUNT ON;

	select count(*) from dbo.spread_distribution_job
	where JobStatus = 'InProgress'
	and OrderId = @orderId
	and TradeId = @tradeId

END

