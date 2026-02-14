-- Create Database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'RemoteDesktopDB')
BEGIN
    CREATE DATABASE RemoteDesktopDB;
END
GO

USE RemoteDesktopDB;
GO

-- Create Users Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Username NVARCHAR(50) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(256) NOT NULL, -- SHA256
        CreatedAt DATETIME DEFAULT GETDATE()
    );

    -- Insert Default Admin User (Password: admin123)
    -- Hash: 240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9 (SHA256 of 'admin123')
    INSERT INTO Users (Username, PasswordHash)
    VALUES ('admin', '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9');
END
GO

-- Create SessionHistory Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SessionHistory')
BEGIN
    CREATE TABLE SessionHistory (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Username NVARCHAR(50) NOT NULL,
        ClientIP NVARCHAR(50),
        StartTime DATETIME DEFAULT GETDATE(),
        EndTime DATETIME,
        FOREIGN KEY (Username) REFERENCES Users(Username)
    );
END
GO
