-- =============================================
-- Author:		Basil Thomas
-- Create date: Nov 13, 2020
-- Description:	delete selected economic calendar event
-- =============================================
CREATE PROCEDURE spDeleteEconomicCalendar 
	-- Add the parameters for the stored procedure here
	@eventDate date,
	@countryCode char(3),
	@eventName varchar(255)
AS
BEGIN
	
	delete from economic_calendar
	where EventDate = @eventDate
	and CountryCode = @countryCode
	and EventName = @eventName

END
