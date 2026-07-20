-- =============================================
-- Author:	Basil Thomas
-- Create date: 2019-03-17
-- Description:	backup fund database
-- =============================================
CREATE PROCEDURE [dbo].[spBackupDatabase]
	-- Add the parameters for the stored procedure here
		@backupType char(4)
AS
BEGIN
	if @backupType = 'full'
		BACKUP DATABASE [funddb] TO  disk = 'e:\backup\funddb-full.bak' WITH  FORMAT, INIT, SKIP, NOREWIND, NOUNLOAD,  STATS = 10
	else
		BACKUP DATABASE [funddb] TO  disk = 'e:\backup\funddb-diff.bak' WITH  DIFFERENTIAL , FORMAT, INIT,  SKIP, NOREWIND, NOUNLOAD,  STATS = 10
END