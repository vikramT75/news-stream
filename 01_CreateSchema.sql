CREATE DATABASE StreamingRagDb;
GO

USE StreamingRagDb;
GO

CREATE TABLE DocumentChunks (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Content NVARCHAR(MAX) NOT NULL,
    Embedding NVARCHAR(MAX) NOT NULL,
    Source NVARCHAR(255),
    IngestionTime DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE OR ALTER PROCEDURE sp_SearchWithTemporalDecay
    @QueryVector NVARCHAR(MAX),
    @DecayRate FLOAT = 0.01,
    @TopK INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @QueryTable TABLE (Idx INT, Val FLOAT);
    
    INSERT INTO @QueryTable (Idx, Val)
    SELECT [key], CAST([value] AS FLOAT) FROM OPENJSON(@QueryVector);

    SELECT TOP (@TopK)
        c.Id,
        c.Content,
        c.Source,
        c.IngestionTime,
        c.Embedding,
        (
            (
                SELECT SUM(q.Val * CAST(v.[value] AS FLOAT))
                FROM OPENJSON(c.Embedding) v
                JOIN @QueryTable q ON v.[key] = q.Idx
            )
            * EXP(-@DecayRate * DATEDIFF(HOUR, c.IngestionTime, SYSUTCDATETIME()))
        ) AS FinalDecayScore
    FROM DocumentChunks c
    ORDER BY FinalDecayScore DESC;
END;
GO
