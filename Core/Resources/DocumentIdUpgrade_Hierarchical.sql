ALTER TABLE [dbo].[DocumentInfos]
	ALTER COLUMN [DocumentID] [nvarchar](260) NOT NULL
GO

DROP PROCEDURE [dbo].[usp_ImportDocumentInfos]
DROP PROCEDURE [dbo].[usp_MarkProcessed]
DROP PROCEDURE [dbo].[usp_SetPriority]
DROP TYPE [dbo].[DocumentInfo];
DROP TYPE [dbo].[ids]
	
CREATE TYPE [dbo].[DocumentInfo] AS TABLE(
	[DocumentID] [nvarchar](260) NOT NULL,
	[ProviderName] [varchar](128) NULL,
	[SubjectID] [int] NOT NULL,
	[GenerationDate] [datetime] NULL
)
GO

CREATE TYPE [dbo].[ids] AS TABLE(
	[ID] [nvarchar](260) NOT NULL
)
GO

CREATE PROCEDURE [dbo].[usp_ImportDocumentInfos]
	@Items AS dbo.DocumentInfo READONLY
AS
BEGIN

	SET XACT_ABORT ON;

	MERGE dbo.DocumentInfos AS dest
	USING @Items AS src
	ON dest.DocumentID = src.DocumentID
	WHEN NOT MATCHED THEN
		INSERT ( DocumentID, SubjectID, GenerationDate, Imported, IsPriority )
		VALUES ( src.DocumentID, src.SubjectID, src.GenerationDate, 0, 0 );

END
GO

CREATE PROCEDURE [dbo].[usp_MarkProcessed]
	@Items AS dbo.ids READONLY
AS
BEGIN

	SET XACT_ABORT ON;

	UPDATE [dbo].[DocumentInfos]
	SET Imported = 1
	FROM @Items
	WHERE [DocumentInfos].DocumentID = [@Items].ID

END
GO

CREATE PROCEDURE [dbo].[usp_SetPriority]
	@Items AS dbo.ids READONLY
AS
BEGIN

	SET XACT_ABORT ON;
	BEGIN TRANSACTION;

        IF EXISTS(SELECT TOP 1 * FROM @Items)
        BEGIN

            UPDATE dbo.DocumentInfos
		    SET IsPriority = 0;

		    UPDATE dbo.DocumentInfos
		    SET IsPriority = 1
		    FROM @Items
		    WHERE DocumentInfos.DocumentID = [@Items].ID

        END
        ELSE
        BEGIN

            UPDATE dbo.DocumentInfos    
		    SET IsPriority = 1;

        END

	COMMIT TRANSACTION;

END
GO
