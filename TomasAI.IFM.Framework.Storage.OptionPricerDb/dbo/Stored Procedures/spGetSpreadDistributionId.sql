-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetSpreadDistributionId] 
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@tradeStatus varchar(64),
	@valueDate datetime
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select Id from dbo.spread_distribution
	where CreatedOn = (select max(CreatedOn) from dbo.spread_distribution
						where TradeId = @tradeId and TradeStatus = @tradeStatus	and ValueDate = @valueDate)

END
