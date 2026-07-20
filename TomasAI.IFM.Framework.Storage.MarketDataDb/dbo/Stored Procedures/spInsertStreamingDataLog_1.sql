-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spInsertStreamingDataLog 
	-- Add the parameters for the stored procedure here
	@valueDate datetime,
	@errorCode int,
	@errorMessage varchar(4000)

AS
BEGIN
	insert into dbo.streaming_data_log(
		ValueDate,
		ErrorCode,
		ErrorMessage
	) values (
		@valueDate,
		@errorCode,
		@errorMessage
	)
END
