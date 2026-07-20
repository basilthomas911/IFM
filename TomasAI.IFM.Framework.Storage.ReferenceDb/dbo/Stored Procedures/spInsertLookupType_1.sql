-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertLookupType] 
	-- Add the parameters for the stored procedure here
	@lookupTypeName varchar(64),
	@shortCode varchar(32),
	@orderid int,
	@description varchar(256),
	@createdOn datetime,
	@createdBy varchar(64)
AS
BEGIN
	
	insert into dbo.lookup_type (
		LookupTypeName,
		ShortCode,
		OrderId,
		Description,
		CreatedOn,
		CreatedBy
	) values (
		@lookupTypeName,
		@shortCode,
		@orderId,
		@description,
		@createdOn,
		@createdBy
	)

END
