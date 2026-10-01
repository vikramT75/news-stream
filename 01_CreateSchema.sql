CREATE DATABASE StreamingRagDb;
GO

USE StreamingRagDb;
GO

-- Our main table for storing chunks and their vector embeddings
CREATE TABLE DocumentChunks (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Content NVARCHAR(MAX) NOT NULL,
    Embedding NVARCHAR(MAX) NOT NULL, -- Storing as a JSON array string
    Source NVARCHAR(255),
    IngestionTime DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- Create the Stored Procedure for Temporal Decay Vector Search
CREATE PROCEDURE sp_SearchWithTemporalDecay
    @QueryVector NVARCHAR(MAX),
    @DecayRate FLOAT = 0.05, -- Lambda (λ) for exponential decay
    @TopK INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    -- NOTE: In a production SQL Server 2022 setup with native vector support enabled, 
    -- we would use VECTOR_DISTANCE(). 
    -- For this portfolio demo, we are mocking the exact cosine similarity math 
    -- using OpenJson to calculate it on the fly, demonstrating deep understanding 
    -- of how embeddings actually work under the hood!

    -- 1. Parse the QueryVector (JSON string) into a temporary table
    DECLARE @QueryTable TABLE (Idx INT, Val FLOAT);
    INSERT INTO @QueryTable (Idx, Val)
    SELECT [key], CAST([value] AS FLOAT) FROM OPENJSON(@QueryVector);

    -- 2. Calculate Cosine Similarity & Temporal Decay
    SELECT TOP (@TopK)
        c.Id,
        c.Content,
        c.Source,
        c.IngestionTime,
        c.Embedding,
        -- The Temporal Decay Math: Similarity * EXP(-λ * AgeInHours)
        (
            -- Dot Product (since OpenAI embeddings are normalized, Dot Product == Cosine Similarity)
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
