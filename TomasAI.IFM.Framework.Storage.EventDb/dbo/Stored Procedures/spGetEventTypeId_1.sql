-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-03-23
-- Description:	create unique id for each unique event type name
-- =============================================
CREATE PROCEDURE [dbo].[spGetEventTypeId]
	@eventTypeName varchar(255)
AS
BEGIN
    
	-- Insert statements for procedure here
	if not exists(select * from event_type where EventTypeName = @eventTypeName)
		insert into event_type(EventTypeName) values (@eventTypeName)
	select EventTypeId from event_type where EventTypeName = @eventTypeName

END
