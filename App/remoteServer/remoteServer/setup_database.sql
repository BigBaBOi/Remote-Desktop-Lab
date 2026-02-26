-- Create Database
CREATE DATABASE IF NOT EXISTS RemoteDesktopDB;
USE RemoteDesktopDB;

-- Create Users Table
CREATE TABLE IF NOT EXISTS Users (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Username VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash VARCHAR(256) NOT NULL, -- SHA256
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Insert Default Admin User (Password: admin123)
-- Hash: 240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9 (SHA256 of 'admin123')
INSERT IGNORE INTO Users (Username, PasswordHash)
VALUES ('admin', '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9');

-- Create SessionHistory Table
CREATE TABLE IF NOT EXISTS SessionHistory (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    Username VARCHAR(50) NOT NULL,
    ClientIP VARCHAR(50),
    StartTime DATETIME DEFAULT CURRENT_TIMESTAMP,
    EndTime DATETIME,
    FOREIGN KEY (Username) REFERENCES Users(Username)
);
