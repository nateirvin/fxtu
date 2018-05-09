ALTER TABLE [dbo].[DocumentInfos]
	ALTER COLUMN [DocumentID] [nvarchar](260) NOT NULL
GO

ALTER TABLE [dbo].[DocumentVariables]
	ALTER COLUMN [DocumentID] [nvarchar](260) NOT NULL
GO

DROP PROCEDURE [dbo].[usp_ImportDocumentInfos];
DROP PROCEDURE [dbo].[usp_InsertVariables]
DROP PROCEDURE [dbo].[usp_MarkProcessed]
DROP PROCEDURE dbo.usp_ReprocessDocuments
GO

DROP TYPE [dbo].[DocumentInfo]
CREATE TYPE [dbo].[DocumentInfo] AS TABLE
(
	[DocumentID] [nvarchar](260) NOT NULL,
	[ProviderName] [varchar](128) NOT NULL,
	[SubjectID] [int] NOT NULL,
	[GenerationDate] [datetime] NULL
)
GO

DROP TYPE [dbo].[DocumentVariable]
CREATE TYPE [dbo].[DocumentVariable] AS TABLE
(
	[DocumentID] [nvarchar](260) NOT NULL,
	[VariableName] [nvarchar](512) NOT NULL,
	[VariableValue] [nvarchar](max) NULL
)
GO

DROP TYPE [dbo].[ids]
CREATE TYPE [dbo].[ids] AS TABLE
(
	[ID] [nvarchar](260) NOT NULL
)
GO

CREATE PROCEDURE [dbo].[usp_ImportDocumentInfos]
	@Items AS dbo.DocumentInfo READONLY
AS
BEGIN

	SET XACT_ABORT ON;

	BEGIN TRANSACTION;

		MERGE [dbo].[Providers] AS dest
		USING 
		(
			SELECT DISTINCT ProviderName
			FROM @Items
		) AS src
		ON dest.ProviderName = src.ProviderName
		WHEN NOT MATCHED THEN
			INSERT ( ProviderName )
			VALUES ( src.ProviderName )
		;

		MERGE [dbo].[DocumentInfos]	AS dest
		USING 
		(
			SELECT 
				DocumentID,
				ProviderID,
				SubjectID,
				GenerationDate
			FROM @Items
				JOIN dbo.Providers
					ON [@Items].ProviderName = Providers.ProviderName
		) AS src
		ON dest.DocumentID = src.DocumentID
		WHEN NOT MATCHED THEN
			INSERT ( DocumentID, ProviderID, SubjectID, GenerationDate, Imported, IsPriority )
			VALUES ( src.DocumentID, src.ProviderID, src.SubjectId, src.GenerationDate, 0, 0 )
		;

	COMMIT TRANSACTION;

END
GO

CREATE PROCEDURE [dbo].[usp_InsertVariables]
	@DocumentVariables AS dbo.DocumentVariable READONLY
AS
BEGIN

	SET XACT_ABORT ON;

	MERGE [dbo].[Variables] AS dest
	USING 
	(
		SELECT DISTINCT VariableName
		FROM @DocumentVariables
	) AS src
	ON dest.VariableName = src.VariableName
	WHEN NOT MATCHED THEN
		INSERT ( VariableName )
		VALUES ( src.VariableName )
	;

	INSERT [dbo].[DocumentVariables] 
		( DocumentID, VariableID, VariableValue )
	SELECT	
		DocumentID,
		VariableID,
		VariableValue 
	FROM @DocumentVariables
		JOIN dbo.Variables
			ON [@DocumentVariables].VariableName = Variables.VariableName

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

CREATE PROCEDURE dbo.usp_ReprocessDocuments
	@Documents AS dbo.ids READONLY
AS
BEGIN

	SET XACT_ABORT ON;

	BEGIN TRANSACTION;

		DELETE dbo.DocumentVariables
		WHERE DocumentID IN (SELECT ID FROM @Documents)

		DELETE dbo.Variables
		WHERE VariableID IN
		(
			SELECT Variables.VariableID
			FROM dbo.Variables
				LEFT JOIN dbo.DocumentVariables
					ON Variables.VariableID = DocumentVariables.VariableID
			GROUP BY Variables.VariableID
			HAVING COUNT(DocumentVariables.DocumentID) = 0
		);

		UPDATE dbo.DocumentInfos
		SET Imported = 0
		FROM @Documents
		WHERE DocumentInfos.DocumentID = [@Documents].ID

	COMMIT TRANSACTION;

END
GO
