-- =============================================
-- Author:		Basil Thomas	
-- Create date: Feb-24-2021
-- Description:	delete event log item
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteEventLog]
	-- Add the parameters for the stored procedure here
AS
BEGIN
	
	delete from event_log
END
