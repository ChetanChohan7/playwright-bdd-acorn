-- Creates the two tables this solution reads from and writes to.
-- Run once against the target database before using the project.

IF OBJECT_ID('dbo.XML_Requests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.XML_Requests
    (
        Scenario_id  NVARCHAR(100)  NOT NULL,
        Quote_ref    NVARCHAR(100)  NOT NULL,
        XML_Request  NVARCHAR(MAX)  NOT NULL,
        CONSTRAINT PK_XML_Requests PRIMARY KEY (Scenario_id, Quote_ref)
    );
END;
GO

-- Append-only history: a re-run of the same scenario_id can carry a new quote_ref, and
-- ResponseDataReader needs "the previous response for this scenario_id" regardless of
-- quote_ref, so rows are inserted rather than upserted and ordered by Created_date to find the
-- latest one.
IF OBJECT_ID('dbo.XML_Response', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.XML_Response
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL,
        Scenario_id   NVARCHAR(100)   NOT NULL,
        Quote_ref     NVARCHAR(100)   NOT NULL,
        XML_Response  NVARCHAR(MAX)   NOT NULL,
        Status        NVARCHAR(10)    NOT NULL,
        Created_date  DATETIME2       NOT NULL CONSTRAINT DF_XML_Response_CreatedDate DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_XML_Response PRIMARY KEY (Id),
        CONSTRAINT CK_XML_Response_Status CHECK (Status IN ('PASS', 'FAIL'))
    );
    CREATE INDEX IX_XML_Response_Scenario_Created ON dbo.XML_Response (Scenario_id, Created_date DESC, Id DESC);
END;
GO
