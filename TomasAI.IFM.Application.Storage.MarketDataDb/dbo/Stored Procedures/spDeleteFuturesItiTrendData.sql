-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spDeleteFuturesItiTrendData 
	-- Add the parameters for the stored procedure here
	@symbol varchar(10)
AS
BEGIN

	delete from futures_iti_trend_data
	where Symbol = @symbol

END
