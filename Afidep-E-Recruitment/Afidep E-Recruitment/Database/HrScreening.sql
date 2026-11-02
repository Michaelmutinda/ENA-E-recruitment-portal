-- HR screening (longlisting / shortlisting) tables, in the portal's database (the one holding Recruitment_DummyData).
-- Safe to run more than once. Add HR users with the portal's password hashing (see Recruitment_HrUsers below).

IF OBJECT_ID('dbo.Recruitment_HrUsers') IS NULL
CREATE TABLE dbo.Recruitment_HrUsers (
    Id int IDENTITY PRIMARY KEY,
    Username nvarchar(100) NOT NULL UNIQUE,
    FullName nvarchar(200) NOT NULL,
    PasswordHash nvarchar(max) NOT NULL,          -- PasswordHashers.HashPassword (PBKDF2, salt inside the hash)
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime NOT NULL DEFAULT GETDATE());

-- each job's screening criteria (JSON of ScreeningCriteria), pre-filled from the advert and edited by HR
IF OBJECT_ID('dbo.Recruitment_JobCriteria') IS NULL
CREATE TABLE dbo.Recruitment_JobCriteria (
    RequestId nvarchar(20) NOT NULL PRIMARY KEY,
    CriteriaJson nvarchar(max) NOT NULL,
    UpdatedBy nvarchar(200) NULL,
    UpdatedAt datetime NOT NULL DEFAULT GETDATE());

-- every longlisting / shortlisting run, with the criteria it used
IF OBJECT_ID('dbo.Recruitment_ScreeningRuns') IS NULL
CREATE TABLE dbo.Recruitment_ScreeningRuns (
    Id int IDENTITY PRIMARY KEY,
    RequestId nvarchar(20) NOT NULL,
    Stage nvarchar(20) NOT NULL,                  -- Longlist / Shortlist
    ShortlistMode nvarchar(10) NULL,              -- Number / Percent
    ShortlistValue decimal(9, 2) NULL,
    CriteriaJson nvarchar(max) NOT NULL,
    Considered int NOT NULL, Longlisted int NOT NULL, Shortlisted int NOT NULL,
    RunBy nvarchar(200) NULL,
    RunAt datetime NOT NULL DEFAULT GETDATE());

IF OBJECT_ID('dbo.Recruitment_ScreeningResults') IS NULL
CREATE TABLE dbo.Recruitment_ScreeningResults (
    Id int IDENTITY PRIMARY KEY,
    RunId int NOT NULL REFERENCES dbo.Recruitment_ScreeningRuns(Id),
    ApplicationNo nvarchar(20) NOT NULL,
    ApplicantId nvarchar(256) NULL,
    ApplicantName nvarchar(300) NULL,
    Longlisted bit NOT NULL,
    Score decimal(5, 1) NOT NULL,
    [Rank] int NULL,
    Shortlisted bit NOT NULL,
    Reason nvarchar(max) NULL,
    TieNote nvarchar(max) NULL,
    ChecksJson nvarchar(max) NULL,
    BreakdownJson nvarchar(max) NULL,
    DocumentsUploaded int NOT NULL DEFAULT 0,
    DocumentsRead int NOT NULL DEFAULT 0,
    DocumentProblems nvarchar(max) NULL);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Recruitment_ScreeningResults_Run')
CREATE INDEX IX_Recruitment_ScreeningResults_Run ON dbo.Recruitment_ScreeningResults (RunId);
