-- =============================================
-- Author:		Basil Thomas
-- Create date: Jan 22, 2022
-- Description:	get all economic calendar events
-- =============================================
CREATE PROCEDURE [dbo].[spGetEconomicCalendarsAll]
	-- Add the parameters for the stored procedure here

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
	order by EventDate

END