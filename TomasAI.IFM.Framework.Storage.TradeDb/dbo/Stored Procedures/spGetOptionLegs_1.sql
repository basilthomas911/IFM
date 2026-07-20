-- =============================================
-- Author:		basil thomas
-- Create date: 
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spGetOptionLegs
	-- Add the parameters for the stored procedure here
	@tradeId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	select [TradeId]
		  ,[ContractId]
		  ,[Quantity]
		  ,[StrikePrice]
		  ,[OptionLegType]
		  ,[OptionLegAction]
		  ,[CreatedOn]
		  ,[CreatedBy]
		  ,[UpdatedOn]
		  ,[UpdatedBy]
	from [dbo].[option_leg]
	where TradeId = @tradeId

END
