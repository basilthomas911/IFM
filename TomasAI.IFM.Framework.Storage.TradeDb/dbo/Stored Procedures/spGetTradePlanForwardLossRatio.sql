-- =============================================
-- Author:		basil thomas
-- Create date: 2019-09-28
-- Description:	get trade plan loss probability
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradePlanForwardLossRatio] 
	-- Add the parameters for the stored procedure here
	@startDate datetime,
	@endDate datetime
AS
BEGIN
	
	-- Insert statements for procedure here
	--SELECT case when ForwardLossRatio > 1.0 then 1.0 else ForwardLossRatio end as ForwardLossRatio
	SELECT ForwardLossRatio
	FROM [dbo].[trade_plan]
	where ValueDate between @startDate and @endDate

END
