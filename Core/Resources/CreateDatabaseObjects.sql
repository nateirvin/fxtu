CREATE TABLE [dbo].[DocumentInfos]
(
	[DocumentID] [int] NOT NULL,
	[SubjectID] [int] NOT NULL,
	[GenerationDate] [datetime] NULL,
	[Imported] [bit] NOT NULL,
	[IsPriority] [bit] NOT NULL,
	CONSTRAINT [PK_OriginInfo] PRIMARY KEY CLUSTERED 
	(
		[DocumentID] ASC
	) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

CREATE TABLE [dbo].[OriginalNames]
(
	[TableName] [sysname] NOT NULL,
	[ColumnName] [sysname] NOT NULL,
	[OriginalName] [nvarchar](512) NOT NULL,
	CONSTRAINT [PK_OriginalNames] PRIMARY KEY CLUSTERED 
	(
		[TableName] ASC,
		[ColumnName] ASC
	) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE dbo.OriginalNames ADD CONSTRAINT CK_RequiredNames CHECK (TableName NOT IN ('') AND OriginalName NOT IN (''))
GO

CREATE TYPE [dbo].[DocumentInfo] AS TABLE(
	[DocumentID] [int] NOT NULL,
	[ProviderName] [varchar](128) NULL,
	[SubjectID] [int] NOT NULL,
	[GenerationDate] [datetime] NULL
)
GO

CREATE TYPE [dbo].[ids] AS TABLE(
	[ID] [int] NOT NULL
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

CREATE PROCEDURE dbo.usp_GetTablesAndColumns
AS
BEGIN

	SELECT 
		tables.object_id,		
		schemas.name AS SchemaName,
		tables.name AS TableName
	INTO #objs
	FROM sys.schemas
		JOIN sys.tables
			ON schemas.schema_id = tables.schema_id
	WHERE schemas.name NOT IN ('sys', 'INFORMATION_SCHEMA')	
		AND tables.name NOT IN ('sysdiagrams')

	SELECT 
		object_id,
		SchemaName,
		TableName		 
	FROM #objs;

	WITH PrimaryKeyColumns AS
	(
		SELECT index_columns.object_id, index_columns.column_id 
		FROM sys.key_constraints
			JOIN sys.index_columns
				ON key_constraints.parent_object_id = index_columns.object_id		
		WHERE key_constraints.[type] = 'PK'
	)
	SELECT DISTINCT
		#objs.object_id,
		columns.name AS ColumnName,
		types.name AS TypeName,
		CASE WHEN types.name LIKE '%char' OR types.name LIKE '%text' OR types.name = 'sysname'
				THEN 
					CASE WHEN columns.max_length = -1 THEN 2147483647
		 				ELSE CASE WHEN types.name LIKE 'n%' OR types.name = 'sysname' THEN columns.max_length / 2 ELSE columns.max_length END
						END
				ELSE NULL END AS MaxCharacters,
		columns.is_nullable,
		CAST(CASE WHEN PrimaryKeyColumns.column_id IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsPrimaryKey,	
		columns.is_identity,
		identity_columns.last_value,
		identity_columns.increment_value
	FROM #objs
		JOIN sys.columns
			ON #objs.object_id = columns.object_id
		JOIN sys.types
			ON columns.user_type_id = types.user_type_id
		LEFT JOIN PrimaryKeyColumns
			ON #objs.object_id = PrimaryKeyColumns.object_id
			AND columns.column_id = PrimaryKeyColumns.column_id
		LEFT JOIN sys.identity_columns
			ON #objs.object_id = identity_columns.object_id
			AND columns.column_id = identity_columns.column_id
			AND columns.is_identity = 1	

	DROP TABLE #objs

END
GO

CREATE PROCEDURE dbo.usp_GetBatchToProcess
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

CREATE PROCEDURE dbo.usp_GetExtendedProperties
AS
BEGIN

	SELECT name, value 
	FROM sys.extended_properties
	WHERE class_desc = 'DATABASE'

END
GO
