-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spInsertFund 
	-- Add the parameters for the stored procedure here
	@fundId int,
	@name varchar(64),
	@description varchar(4000),
	@balance money,
	@createdOn datetime,
	@createdBy varchar(256)
AS
BEGIN
	
	if not exists(select * from dbo.fund where FundId = @fundId)
		insert into dbo.fund(
			FundId,
			[Name],
			[Description],
			Balance,
			CreatedOn,
			CreatedBy
		) values (
			@fundId,
			@name,
			@description,
			@balance,
			@createdOn,
			@createdBy
		)

END
