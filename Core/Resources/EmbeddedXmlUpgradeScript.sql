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

DECLARE @Documents AS dbo.ids

INSERT INTO @Documents ( ID )
/* QUERY */

EXEC dbo.usp_ReprocessDocuments @Documents
GO
