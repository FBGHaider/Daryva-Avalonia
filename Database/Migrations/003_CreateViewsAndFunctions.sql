-- =============================================
-- LandLord Buddy Database - Views and Helper Functions
-- =============================================

USE [LandLordBuddyDB]
GO

-- =============================================
-- View: Active Tenancies with Tenant and House Info
-- =============================================
IF OBJECT_ID('dbo.vw_ActiveTenancies', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_ActiveTenancies];
GO

CREATE VIEW [dbo].[vw_ActiveTenancies]
AS
SELECT 
    t.[TenancyId],
    t.[HouseId],
    t.[TenantId],
    h.[AddressLine1],
    h.[AddressLine2],
    h.[City],
    h.[Postcode],
    h.[TotalRooms],
    tn.[FullName] AS [TenantName],
    tn.[Email] AS [TenantEmail],
    tn.[PhoneNumber] AS [TenantPhone],
    t.[MoveInDate],
    t.[MoveOutDate],
    t.[RentAmountMonthly],
    t.[DepositAmount],
    t.[PaymentDueDay],
    t.[Status],
    t.[Notes]
FROM [dbo].[Tenancy] t
INNER JOIN [dbo].[House] h ON t.[HouseId] = h.[HouseId]
INNER JOIN [dbo].[Tenant] tn ON t.[TenantId] = tn.[TenantId]
WHERE t.[Status] = 'Active' AND tn.[IsArchived] = 0;
GO

PRINT 'View vw_ActiveTenancies created.';
GO

-- =============================================
-- View: House Summary with Active Tenant Count
-- =============================================
IF OBJECT_ID('dbo.vw_HouseSummary', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_HouseSummary];
GO

CREATE VIEW [dbo].[vw_HouseSummary]
AS
SELECT 
    h.[HouseId],
    h.[AddressLine1],
    h.[AddressLine2],
    h.[City],
    h.[Postcode],
    h.[TotalRooms],
    h.[CreatedAt],
    COUNT(DISTINCT CASE WHEN t.[Status] = 'Active' THEN t.[TenantId] END) AS [ActiveTenantCount],
    SUM(CASE WHEN t.[Status] = 'Active' THEN t.[RentAmountMonthly] ELSE 0 END) AS [TotalMonthlyRent]
FROM [dbo].[House] h
LEFT JOIN [dbo].[Tenancy] t ON h.[HouseId] = t.[HouseId]
LEFT JOIN [dbo].[Tenant] tn ON t.[TenantId] = tn.[TenantId] AND tn.[IsArchived] = 0
GROUP BY 
    h.[HouseId],
    h.[AddressLine1],
    h.[AddressLine2],
    h.[City],
    h.[Postcode],
    h.[TotalRooms],
    h.[CreatedAt];
GO

PRINT 'View vw_HouseSummary created.';
GO

-- =============================================
-- View: Current Month Rent Status
-- =============================================
IF OBJECT_ID('dbo.vw_CurrentMonthRentStatus', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_CurrentMonthRentStatus];
GO

CREATE VIEW [dbo].[vw_CurrentMonthRentStatus]
AS
SELECT 
    rc.[RentChargeId],
    rc.[TenancyId],
    t.[HouseId],
    t.[TenantId],
    h.[AddressLine1] + ISNULL(', ' + h.[AddressLine2], '') + ', ' + h.[City] AS [HouseAddress],
    tn.[FullName] AS [TenantName],
    rc.[PeriodYear],
    rc.[PeriodMonth],
    rc.[AmountDue],
    rc.[DueDate],
    rc.[Status] AS [ChargeStatus],
    ISNULL(SUM(rp.[AmountPaid]), 0) AS [TotalPaid],
    rc.[AmountDue] - ISNULL(SUM(rp.[AmountPaid]), 0) AS [OutstandingAmount],
    CASE 
        WHEN ISNULL(SUM(rp.[AmountPaid]), 0) >= rc.[AmountDue] THEN 'Paid'
        WHEN ISNULL(SUM(rp.[AmountPaid]), 0) > 0 THEN 'PartPaid'
        WHEN rc.[DueDate] < CAST(GETDATE() AS DATE) THEN 'Late'
        ELSE 'Unpaid'
    END AS [PaymentStatus]
FROM [dbo].[RentCharge] rc
INNER JOIN [dbo].[Tenancy] t ON rc.[TenancyId] = t.[TenancyId]
INNER JOIN [dbo].[House] h ON t.[HouseId] = h.[HouseId]
INNER JOIN [dbo].[Tenant] tn ON t.[TenantId] = tn.[TenantId]
LEFT JOIN [dbo].[RentPayment] rp ON rc.[RentChargeId] = rp.[RentChargeId]
WHERE rc.[PeriodYear] = YEAR(GETDATE()) 
    AND rc.[PeriodMonth] = MONTH(GETDATE())
GROUP BY 
    rc.[RentChargeId],
    rc.[TenancyId],
    t.[HouseId],
    t.[TenantId],
    h.[AddressLine1],
    h.[AddressLine2],
    h.[City],
    tn.[FullName],
    rc.[PeriodYear],
    rc.[PeriodMonth],
    rc.[AmountDue],
    rc.[DueDate],
    rc.[Status];
GO

PRINT 'View vw_CurrentMonthRentStatus created.';
GO

-- =============================================
-- Function: Get Active Tenant Count for House
-- =============================================
IF OBJECT_ID('dbo.fn_GetActiveTenantCount', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_GetActiveTenantCount];
GO

CREATE FUNCTION [dbo].[fn_GetActiveTenantCount](@HouseId INT)
RETURNS INT
AS
BEGIN
    DECLARE @Count INT;
    
    SELECT @Count = COUNT(DISTINCT t.[TenantId])
    FROM [dbo].[Tenancy] t
    INNER JOIN [dbo].[Tenant] tn ON t.[TenantId] = tn.[TenantId]
    WHERE t.[HouseId] = @HouseId 
        AND t.[Status] = 'Active' 
        AND tn.[IsArchived] = 0;
    
    RETURN ISNULL(@Count, 0);
END;
GO

PRINT 'Function fn_GetActiveTenantCount created.';
GO

-- =============================================
-- Function: Check if Rent is Paid for Period
-- =============================================
IF OBJECT_ID('dbo.fn_IsRentPaid', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_IsRentPaid];
GO

CREATE FUNCTION [dbo].[fn_IsRentPaid](
    @TenancyId INT,
    @PeriodYear INT,
    @PeriodMonth INT
)
RETURNS BIT
AS
BEGIN
    DECLARE @TotalPaid DECIMAL(10,2);
    DECLARE @AmountDue DECIMAL(10,2);
    
    SELECT @AmountDue = [AmountDue]
    FROM [dbo].[RentCharge]
    WHERE [TenancyId] = @TenancyId
        AND [PeriodYear] = @PeriodYear
        AND [PeriodMonth] = @PeriodMonth;
    
    IF @AmountDue IS NULL
        RETURN 0;
    
    SELECT @TotalPaid = ISNULL(SUM([AmountPaid]), 0)
    FROM [dbo].[RentPayment]
    WHERE [RentChargeId] IN (
        SELECT [RentChargeId]
        FROM [dbo].[RentCharge]
        WHERE [TenancyId] = @TenancyId
            AND [PeriodYear] = @PeriodYear
            AND [PeriodMonth] = @PeriodMonth
    );
    
    RETURN CASE WHEN @TotalPaid >= @AmountDue THEN 1 ELSE 0 END;
END;
GO

PRINT 'Function fn_IsRentPaid created.';
GO

PRINT 'All views and functions created successfully!';
GO
