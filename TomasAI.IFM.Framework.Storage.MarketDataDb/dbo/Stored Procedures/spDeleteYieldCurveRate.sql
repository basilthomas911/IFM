-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spDeleteYieldCurveRate 
	-- Add the parameters for the stored procedure here
	@valueDate date
AS
BEGIN
	delete from dbo.yield_curve_rates 
	where ValueDate = @valueDate
END
