-- =============================================
-- Author:		Basil Thomas
-- Create date: Nov 13, 2020
-- Description:	get all economic calendar events by value date
-- =============================================
Create PROCEDURE [dbo].[spGetEconomicCalendarById]
	-- Add the parameters for the stored procedure here
	@eventDate datetime,
	@countryCode varchar(10),
	@eventName varchar(255)
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
	where EventDate = @eventDate
	and CountryCode = @countryCode
	and EventName = @eventName

END