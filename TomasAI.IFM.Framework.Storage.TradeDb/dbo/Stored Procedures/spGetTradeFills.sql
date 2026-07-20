-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spGetTradeFills 
	-- Add the parameters for the stored procedure here
	@tradeId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT [TradeId]
	  ,[ContractId]
      ,max([FillDate]) as FillDate
      ,avg([Price]) as Price
      ,sum([Quantity]) as Quantity
      ,sum([Commission]) as Commission
      ,max([CreatedOn]) as CreatedOn
      ,max([CreatedBy]) as CreatedBy
	FROM [dbo].[trade_fill]
	where TradeId = @tradeId
	group by TradeId,ContractId


END
