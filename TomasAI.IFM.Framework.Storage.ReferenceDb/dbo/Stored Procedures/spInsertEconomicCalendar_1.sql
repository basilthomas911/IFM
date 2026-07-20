-- =============================================
-- Author:		Basil Thomas
-- Create date: Nov 13, 2020
-- Description:	insert economic calendar events
-- =============================================
CREATE PROCEDURE [dbo].[spInsertEconomicCalendar] 
	-- Add the parameters for the stored procedure here
	@eventDate datetime,
	@countryCode char(3),
	@eventName varchar(255),
	@actual varchar(50) = null,
	@forecast varchar(50) = null,
	@prior varchar(50) = null,
	@createdOn datetime,
	@createdBy varchar(255) = null
AS
BEGIN

	if not exists(select * from economic_calendar where EventDate = @eventDate and CountryCode = @countryCode and EventName = @eventName )
	begin
		insert into economic_calendar(
			EventDate,
			CountryCode,
			EventName,
			Actual,
			Forecast,
			[Prior],
			CreatedOn,
			CreatedBy
		) values (
			@eventDate,
			@countryCode,
			@eventName,
			@actual,
			@forecast,
			@prior,
			@createdOn,
			@createdBy
		)
	end
END
