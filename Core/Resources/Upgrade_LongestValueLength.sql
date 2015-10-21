ALTER TABLE dbo.Variables ADD LongestValueLength INT NULL;
GO

DROP PROCEDURE [dbo].[usp_UpdateDataKinds];
GO

DROP TYPE [dbo].[Variable]
GO

CREATE TYPE [dbo].[Variable] AS TABLE(
	[VariableName] [nvarchar](512) NOT NULL,
	[DataKind] [varchar](50) NOT NULL,
	[LongestValueLength] [int] NULL
);
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
