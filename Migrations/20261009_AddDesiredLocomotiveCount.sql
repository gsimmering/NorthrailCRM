IF COL_LENGTH(N'dbo.t_CRM_Leads', N'DesiredLocomotiveCount') IS NULL
BEGIN
    ALTER TABLE dbo.t_CRM_Leads
    ADD DesiredLocomotiveCount int NOT NULL
        CONSTRAINT DF_t_CRM_Leads_DesiredLocomotiveCount DEFAULT (1);
END;