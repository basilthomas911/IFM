
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetMarketHolidays] 
	@currencyType char(3)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    SELECT  
		CurrencyType,
        HolidayDate,
        Description
    FROM dbo.market_holidays
	where CurrencyType = @currencyType
END
