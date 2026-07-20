-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertCommandLog] 
	@commandId varchar(64),
	@entityId varchar(4000),
    @commandTypeName varchar(4000),
    @commandData text
AS
BEGIN
	insert into dbo.command_log (
		CommandId, 
		EntityId,
        CommandTypeName, 
        CommandData)
    values (
		@commandId, 
        @entityId, 
        @commandTypeName,
		@commandData);

END
