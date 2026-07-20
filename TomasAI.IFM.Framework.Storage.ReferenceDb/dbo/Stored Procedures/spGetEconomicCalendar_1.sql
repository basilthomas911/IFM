-- =============================================
-- Author:		Basil Thomas
-- Create date: Nov 13, 2020
-- Description:	get all economic calendar events by value date
-- =============================================
CREATE PROCEDURE [dbo].[spGetEconomicCalendar]
	-- Add the parameters for the stored procedure here
	@startDate datetime,
	@endDate datetime
AS
BEGIN
	
	select [EventDate]
       ,[CountryCode]
      ,[EventName]
      ,[Actual]
      ,[Forecast]
      ,[Prior]
      ,[CreatedOn]
      ,[CreatedBy]
	from [dbo].[economic_calendar]
	where Eventdate between @startDate and @endDate
	order by EventDate

END