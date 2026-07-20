-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spGetMDIForwardLossRatios
	-- Add the parameters for the stored procedure here
	@upTrendTradeType varchar(32),
	@downTrendTradeType varchar(32)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select * from mdi_forward_loss_ratio
	where TradeType = @upTrendTradeType
	union select * from mdi_forward_loss_ratio
	where TradeType = @downTrendTradeType
	order by TrendDirection desc, MDI desc

END
