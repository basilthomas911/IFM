-- =============================================
-- Author:		Basil Thomas	
-- Create date: Feb-24-2021
-- Description:	delete event log item
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteEventLogById]
	-- Add the parameters for the stored procedure here
	@eventId bigint
AS
BEGIN
	
	delete from event_log
	where EventId = @eventId
END
