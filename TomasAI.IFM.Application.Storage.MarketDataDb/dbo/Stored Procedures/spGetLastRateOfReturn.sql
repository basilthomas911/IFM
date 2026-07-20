-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetLastRateOfReturn] 
	-- Add the parameters for the stored procedure here
	@symbol varchar(32)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT 
		Symbol,
		ValueDate,
		RateOfReturn
	from dbo.return_rates
	where Symbol = @symbol
	and ValueDate = (select max(ValueDate) from dbo.return_rates where Symbol = @symbol)
END
