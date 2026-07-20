
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertDenormalizerLog] 
	@commandId varchar(64),
	@denormalizerDate datetime,
    @denormalizerName varchar(4000),
	@denormalizerData text,
	@userName varchar(255),
	@errorMessage varchar(255),
	@errorCode int,
	@errorType varchar(64),
	@errorData text
AS
BEGIN

	insert into dbo.denormalizer_log (
		CommandId, 
		DenormalizerDate,
        DenormalizerName, 
		DenormalizerData,
		UserName,
		ErrorMessage,
		ErrorCode,
		ErrorType,
		ErrorData)
    values (
		@commandId, 
        @denormalizerDate, 
        @denormalizerName,
		@denormalizerData,
		@userName,
		@errorMessage,
		@errorCode,
		@errorType,
		@errorData);

END
