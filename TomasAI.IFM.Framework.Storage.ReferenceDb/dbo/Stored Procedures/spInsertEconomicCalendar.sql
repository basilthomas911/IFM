-- =============================================
-- Author:		Basil Thomas
-- Create date: Nov 13, 2020
-- Description:	insert economic calendar events
-- =============================================
CREATE PROCEDURE [dbo].[spInsertEconomicCalendar] 
	-- Add the parameters for the stored procedure here
	@eventDate date,
	@eventTime datetime,
	@countryCode char(3),
	@eventName varchar(255),
	@description text null,
	@period varchar(64),
	@consensus real = null,
	@actual real = null,
	@prior real = null,
	@createdOn datetime,
	@createdBy varchar(255) = null
AS
BEGIN

	if not exists(select * from economic_calendar where EventDate = @eventDate and CountryCode = @countryCode and EventName = @eventName )
	begin
		insert into economic_calendar(
			EventDate,
			EventTime,
			CountryCode,
			EventName,
			[Description],
			[Period],
			Consensus,
			Actual,
			[Prior],
			CreatedOn,
			CreatedBy
		) values (
			@eventDate,
			@eventTime,
			@countryCode,
			@eventName,
			@description,
			@period,
			@consensus,
			@actual,
			@prior,
			@createdOn,
			@createdBy
		)
	end
END
