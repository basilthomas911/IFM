
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertQueryLog] 
	@commandId varchar(64),
	@queryDate datetime,
	@queryName varchar(4000),
    @queryData text,
	@userName varchar(255),
	@errorMessage varchar(255),
	@errorCode int,
	@errorType varchar(64),
	@errorData text
AS
BEGIN
	insert into dbo.query_log (
		CommandId, 
		QueryDate,
        QueryName, 
        QueryData,
		UserName,
		ErrorMessage,
		ErrorCode,
		ErrorType,
		ErrorData)
    values (
		@commandId, 
        @queryDate, 
        @queryName,
		@queryData,
		@userName,
		@errorMessage,
		@errorCode,
		@errorType, 
		@errorData);

END
