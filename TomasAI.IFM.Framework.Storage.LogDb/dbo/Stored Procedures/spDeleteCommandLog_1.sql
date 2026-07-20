-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spDeleteCommandLog 
	-- Add the parameters for the stored procedure here
	@commandId varchar(64)
AS
BEGIN
	delete from dbo.command_log 
	where CommandId = @commandId
END
