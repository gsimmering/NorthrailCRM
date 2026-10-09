IF OBJECT_ID(N'dbo.t_CRM_Leads', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.t_CRM_Leads
    (
        LeadId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_t_CRM_Leads PRIMARY KEY,
        CreatedAtUtc datetime2(0) NOT NULL CONSTRAINT DF_t_CRM_Leads_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CompanyName nvarchar(250) NOT NULL,
        FirstName nvarchar(100) NOT NULL,
        LastName nvarchar(100) NOT NULL,
        Email nvarchar(320) NOT NULL,
        PhoneNumber nvarchar(50) NOT NULL,
        Role nvarchar(120) NOT NULL,
        SafetyCertificates nvarchar(max) NOT NULL CONSTRAINT DF_t_CRM_Leads_SafetyCertificates DEFAULT N'[]',
        TractionType nvarchar(120) NOT NULL,
        LocomotivePlatform nvarchar(180) NOT NULL,
        OtherPlatform nvarchar(250) NOT NULL,
        Corridors nvarchar(max) NOT NULL CONSTRAINT DF_t_CRM_Leads_Corridors DEFAULT N'[]',
        SafetySystems nvarchar(max) NOT NULL CONSTRAINT DF_t_CRM_Leads_SafetySystems DEFAULT N'[]',
        RadioRemoteControl bit NOT NULL CONSTRAINT DF_t_CRM_Leads_RadioRemoteControl DEFAULT 0,
        LastMileDiesel bit NOT NULL CONSTRAINT DF_t_CRM_Leads_LastMileDiesel DEFAULT 0,
        MiddleCoupler bit NOT NULL CONSTRAINT DF_t_CRM_Leads_MiddleCoupler DEFAULT 0,
        RentalModel nvarchar(120) NOT NULL,
        PlannedDuration nvarchar(120) NOT NULL,
        DesiredAvailability date NULL,
        MonthlyMileage nvarchar(120) NOT NULL,
        Route nvarchar(500) NOT NULL
    );
END;