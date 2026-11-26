-- =============================================================
-- Livestock Health and Veterinary Service Management System
-- SQL Server schema (simplified, matches the project proposal)
-- =============================================================

USE [master];
GO

IF DB_ID(N'Livestock_Health_DB') IS NULL
    CREATE DATABASE [Livestock_Health_DB];
GO

USE [Livestock_Health_DB];
GO

-- -------------------------------------------------------------
-- Clinics
-- -------------------------------------------------------------
CREATE TABLE dbo.Clinics (
    clinic_ID      INT IDENTITY(1,1) NOT NULL,
    clinic_name    NVARCHAR(100) NOT NULL,
    district       NVARCHAR(100) NOT NULL,
    sector         NVARCHAR(100) NOT NULL,
    cell           NVARCHAR(100) NOT NULL,
    village        NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_Clinics PRIMARY KEY (clinic_ID)
);
GO

-- -------------------------------------------------------------
-- Users (Farmer, Veterinarian, Admin)
-- clinic_ID is only set for veterinarians (FR05: search vets by clinic location)
-- -------------------------------------------------------------
CREATE TABLE dbo.Users (
    user_ID        INT IDENTITY(1,1) NOT NULL,
    full_name      NVARCHAR(100) NOT NULL,
    email          NVARCHAR(100) NOT NULL,
    phone          NVARCHAR(20)  NULL,
    passwordHash   NVARCHAR(255) NOT NULL,
    role           NVARCHAR(20)  NOT NULL,
    clinic_ID      INT NULL,
    CONSTRAINT PK_Users PRIMARY KEY (user_ID),
    CONSTRAINT UQ_Users_Email UNIQUE (email),
    CONSTRAINT FK_Users_Clinics FOREIGN KEY (clinic_ID) REFERENCES dbo.Clinics (clinic_ID),
    CONSTRAINT CK_Users_Role CHECK (role IN ('Farmer', 'Veterinarian', 'Admin'))
);
GO

-- -------------------------------------------------------------
-- Farms (owned by a farmer)
-- -------------------------------------------------------------
CREATE TABLE dbo.Farms (
    farm_ID    INT IDENTITY(1,1) NOT NULL,
    farmer_ID  INT NOT NULL,
    farm_name  NVARCHAR(100) NOT NULL,
    district   NVARCHAR(100) NOT NULL,
    sector     NVARCHAR(100) NOT NULL,
    cell       NVARCHAR(100) NOT NULL,
    village    NVARCHAR(100) NOT NULL,
    CONSTRAINT PK_Farms PRIMARY KEY (farm_ID),
    CONSTRAINT FK_Farms_Users FOREIGN KEY (farmer_ID) REFERENCES dbo.Users (user_ID)
);
GO

-- -------------------------------------------------------------
-- Animals (belong to a farm)
-- -------------------------------------------------------------
CREATE TABLE dbo.Animals (
    animal_ID     INT IDENTITY(1,1) NOT NULL,
    farm_ID       INT NOT NULL,
    species       NVARCHAR(50) NOT NULL,   -- cattle, goat, sheep, pig, poultry
    tag_number    NVARCHAR(50) NOT NULL,   -- unique identification number
    breed         NVARCHAR(50) NULL,
    sex           NVARCHAR(10) NULL,
    date_of_birth DATE NULL,
    CONSTRAINT PK_Animals PRIMARY KEY (animal_ID),
    CONSTRAINT UQ_Animals_TagNumber UNIQUE (tag_number),
    CONSTRAINT FK_Animals_Farms FOREIGN KEY (farm_ID) REFERENCES dbo.Farms (farm_ID)
);
GO

-- -------------------------------------------------------------
-- Appointments (animal + veterinarian)
-- -------------------------------------------------------------
CREATE TABLE dbo.Appointments (
    appointment_ID   INT IDENTITY(1,1) NOT NULL,
    animal_ID        INT NOT NULL,
    veterinarian_ID  INT NOT NULL,
    appointment_date DATETIME NOT NULL,
    status           NVARCHAR(20) NOT NULL CONSTRAINT DF_Appointments_Status DEFAULT 'Pending',
    detail           NVARCHAR(500) NULL,
    CONSTRAINT PK_Appointments PRIMARY KEY (appointment_ID),
    CONSTRAINT FK_Appointments_Animals FOREIGN KEY (animal_ID) REFERENCES dbo.Animals (animal_ID),
    CONSTRAINT FK_Appointments_Users FOREIGN KEY (veterinarian_ID) REFERENCES dbo.Users (user_ID)
);
GO

-- -------------------------------------------------------------
-- HealthRecords (diagnoses, treatments, disease reports)
-- veterinarian_ID is optional: a farmer may report a suspected
-- disease before a vet has examined the animal.
-- -------------------------------------------------------------
CREATE TABLE dbo.HealthRecords (
    record_ID            INT IDENTITY(1,1) NOT NULL,
    animal_ID            INT NOT NULL,
    veterinarian_ID      INT NULL,
    diagnosis            NVARCHAR(200) NULL,
    treatment            NVARCHAR(200) NULL,
    record_date          DATETIME NOT NULL CONSTRAINT DF_HealthRecords_Date DEFAULT GETDATE(),
    reported_by_user_ID  INT NOT NULL,
    case_status          NVARCHAR(50) NULL,
    description          NVARCHAR(500) NULL,
    CONSTRAINT PK_HealthRecords PRIMARY KEY (record_ID),
    CONSTRAINT FK_HealthRecords_Animals FOREIGN KEY (animal_ID) REFERENCES dbo.Animals (animal_ID),
    CONSTRAINT FK_HealthRecords_Vet FOREIGN KEY (veterinarian_ID) REFERENCES dbo.Users (user_ID),
    CONSTRAINT FK_HealthRecords_ReportedBy FOREIGN KEY (reported_by_user_ID) REFERENCES dbo.Users (user_ID)
);
GO

-- -------------------------------------------------------------
-- Vaccinations
-- -------------------------------------------------------------
CREATE TABLE dbo.Vaccinations (
    vaccination_ID    INT IDENTITY(1,1) NOT NULL,
    animal_ID         INT NOT NULL,
    vaccine_name      NVARCHAR(100) NOT NULL,
    vaccination_date  DATE NOT NULL,
    next_due_date     DATE NULL,
    CONSTRAINT PK_Vaccinations PRIMARY KEY (vaccination_ID),
    CONSTRAINT FK_Vaccinations_Animals FOREIGN KEY (animal_ID) REFERENCES dbo.Animals (animal_ID)
);
GO
