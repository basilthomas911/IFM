-- =============================================
-- Author:		Basil Thomas
-- Create date: Nov 13, 2020
-- Description:	get all economic calendar events by value date
-- =============================================
CREATE PROCEDURE [dbo].[spGetEconomicCalendar]
	-- Add the parameters for the stored procedure here
	@eventDate date
AS
BEGIN
	
	select [EventDate]
      ,[EventTime]
      ,[CountryCode]
      ,[EventName]
      ,[Description]
      ,[Period]
      ,[Consensus]
      ,[Actual]
      ,[Prior]
      ,[CreatedOn]
      ,[CreatedBy]
	from [dbo].[economic_calendar]
	where Eventdate = @eventDate
	order by EventTime

END