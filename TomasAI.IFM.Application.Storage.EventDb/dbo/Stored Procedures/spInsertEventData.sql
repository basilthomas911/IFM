-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertEventData] 
	-- Add the parameters for the stored procedure here
	@eventId bigint, 
	@eventData text
AS
BEGIN
	
	if @eventData is not null
		update dbo.event_log
		set EventData = @eventData
		where EventId = @eventId

END
