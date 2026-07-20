-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spInsertStatusConsoleLog 
	-- Add the parameters for the stored procedure here
	@statusDate datetime,
	@statusCode int,
	@source varchar(32),
	@message varchar(256),
	@data text=null,
	@dataType varchar(4000)
AS
BEGIN
	insert into dbo.status_console_log(
		StatusDate,
		StatusCode,
		Source,
		Message,
		Data,
		DataType
	) values (
		@statusDate,
		@statusCode,
		@source,
		@message,
		@data,
		@dataType
	)

END
