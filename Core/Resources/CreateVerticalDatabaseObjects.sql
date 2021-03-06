CREATE TABLE [dbo].[DocumentInfos](
	[DocumentID] [int] NOT NULL,
	[ProviderID] [int] NOT NULL,
	[SubjectID] [int] NOT NULL,
	[GenerationDate] [datetime] NULL,
	[Imported] [bit] NOT NULL,
	[IsPriority] [bit] NOT NULL,
 CONSTRAINT [PK_DocumentInfos] PRIMARY KEY CLUSTERED 
(
	[DocumentID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[DocumentVariables](
	[DocumentID] [int] NOT NULL,
	[VariableID] [int] NOT NULL,
	[VariableValue] [nvarchar](max) NULL,
 CONSTRAINT [PK_DocumentVariables] PRIMARY KEY CLUSTERED 
(
	[DocumentID] ASC,
	[VariableID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_DocumentVariables_VariableID] ON [dbo].[DocumentVariables]
(
	[VariableID] ASC
)
INCLUDE ( [DocumentID] ) 
GO

CREATE TABLE [dbo].[Providers](
	[ProviderID] [int] IDENTITY(1,1) NOT NULL,
	[ProviderName] [varchar](128) NOT NULL,
 CONSTRAINT [PK_Source] PRIMARY KEY CLUSTERED 
(
	[ProviderID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

CREATE TABLE [dbo].[Variables](
	[VariableID] [int] IDENTITY(1,1) NOT NULL,
	[VariableName] [nvarchar](512) NOT NULL,
	[DataKind] [varchar](50) NULL,
	[LongestValueLength] [int] NULL
 CONSTRAINT [PK_Variable] PRIMARY KEY CLUSTERED 
(
	[VariableID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[DocumentInfos]  WITH CHECK ADD  CONSTRAINT [FK_DocumentInfos_Providers] FOREIGN KEY([ProviderID])
REFERENCES [dbo].[Providers] ([ProviderID])
GO

ALTER TABLE [dbo].[DocumentInfos] CHECK CONSTRAINT [FK_DocumentInfos_Providers]
GO

ALTER TABLE [dbo].[DocumentVariables]  WITH CHECK ADD  CONSTRAINT [FK_DocumentVariables_DocumentInfos] FOREIGN KEY([DocumentID])
REFERENCES [dbo].[DocumentInfos] ([DocumentID])
GO

ALTER TABLE [dbo].[DocumentVariables] CHECK CONSTRAINT [FK_DocumentVariables_DocumentInfos]
GO

ALTER TABLE [dbo].[DocumentVariables]  WITH CHECK ADD  CONSTRAINT [FK_DocumentVariables_Variables] FOREIGN KEY([VariableID])
REFERENCES [dbo].[Variables] ([VariableID])
GO

ALTER TABLE [dbo].[DocumentVariables] CHECK CONSTRAINT [FK_DocumentVariables_Variables]
GO

CREATE TYPE [dbo].[DocumentInfo] AS TABLE(
	[DocumentID] [int] NOT NULL,
	[ProviderName] [varchar](128) NOT NULL,
	[SubjectID] [int] NOT NULL,
	[GenerationDate] [datetime] NULL
)
GO

CREATE TYPE [dbo].[DocumentVariable] AS TABLE(
	[DocumentID] [int] NOT NULL,
	[VariableName] [nvarchar](512) NOT NULL,
	[VariableValue] [nvarchar](max) NULL
)
GO

CREATE TYPE [dbo].[Variable] AS TABLE(
	[VariableName] [nvarchar](512) NOT NULL,
	[DataKind] [varchar](50) NOT NULL,
	[LongestValueLength] [int] NULL
)
GO

CREATE TYPE [dbo].[ids] AS TABLE(
	[ID] [int] NOT NULL
)
GO

CREATE PROCEDURE [dbo].[usp_GetBatchToProcess]
	@BatchSize INT
AS
BEGIN

	SELECT TOP (@BatchSize)
		DocumentID
	FROM dbo.DocumentInfos
	WHERE Imported = 0
		AND IsPriority = 1
	ORDER BY GenerationDate

END
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

CREATE PROCEDURE [dbo].[usp_SetPriority]
	@ProviderName VARCHAR(128)
AS
BEGIN

	SET XACT_ABORT ON;
	BEGIN TRANSACTION;

		IF @ProviderName IS NULL
		BEGIN

			UPDATE dbo.DocumentInfos
			SET IsPriority = 1

		END
		ELSE
		BEGIN

			DECLARE @ProviderID INT;
			SELECT @ProviderID = ProviderID 
			FROM dbo.Providers
			WHERE ProviderName = @ProviderName;

			UPDATE dbo.DocumentInfos
			SET IsPriority = 0

			UPDATE dbo.DocumentInfos
			SET IsPriority = 1
			WHERE ProviderID = @ProviderID;

		END

	COMMIT TRANSACTION;

END
GO

CREATE PROCEDURE dbo.usp_GetAllVariables
AS
BEGIN

	SELECT 
		VariableID,
		VariableName,
		DataKind,
		LongestValueLength
	FROM dbo.Variables

END
GO

CREATE PROCEDURE dbo.usp_UpdateVariables
	@Updates AS dbo.[Variable] READONLY
AS
BEGIN

	SET XACT_ABORT ON;

	MERGE dbo.Variables AS dest
	USING @Updates AS src
	ON src.VariableName = dest.VariableName
	WHEN MATCHED THEN
		UPDATE 
		SET DataKind = src.DataKind,
			LongestValueLength = src.LongestValueLength
	;

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

CREATE PROCEDURE dbo.usp_GetExtendedProperties
AS
BEGIN

	SELECT name, value 
	FROM sys.extended_properties
	WHERE class_desc = 'DATABASE'

END
GO
