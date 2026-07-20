-- =============================================
-- Author:	Basil Thomas
-- Create date: 2019-03-17
-- Description:	backup reference database
-- =============================================
CREATE PROCEDURE [dbo].[spBackupDatabase]
	-- Add the parameters for the stored procedure here
	@backupName varchar(256),
	@backupType char(4)
AS
BEGIN
	if not exists (SELECT * FROM sys.credentials WHERE name = 'https://ifmdatastorage.blob.core.windows.net/querydata')  
		CREATE CREDENTIAL [https://ifmdatastorage.blob.core.windows.net/querydata] 
			WITH IDENTITY = 'Shared Access Signature' ,
			SECRET = 'sv=2014-02-14&sr=c&sig=2R87qowUZDqbz8FYr4tjvAzg64LFFGb2v14RyTekjTU%3D&se=2020-12-31T05%3A00%3A00Z&sp=rwdl' 
	if @backupType = 'full'
		BACKUP DATABASE referencedb TO  URL = @backupName WITH  FORMAT, INIT,  NAME = N'referencedb-Full Database Backup', SKIP, NOREWIND, NOUNLOAD,  STATS = 10
	else
		BACKUP DATABASE referencedb TO  URL = @backupName WITH  DIFFERENTIAL , FORMAT, INIT,  NAME = N'referencedb-Full Database Backup', SKIP, NOREWIND, NOUNLOAD,  STATS = 10
END
