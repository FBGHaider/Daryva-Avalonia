-- =============================================
-- Migration: Add RentStartMonth/RentStartYear to Tenancy, backfill MoveInDate from first payment
-- =============================================

-- Add RentStartMonth and RentStartYear (nullable - when null, use MoveInDate for first rent period)
ALTER TABLE Tenancy ADD COLUMN RentStartMonth INTEGER;
ALTER TABLE Tenancy ADD COLUMN RentStartYear INTEGER;

-- Backfill MoveInDate for existing tenancies: use first payment record date
-- Step 1: Tenancies with rent payments - set MoveInDate to earliest RentPayment.PaidOn
UPDATE Tenancy 
SET MoveInDate = (SELECT MIN(PaidOn) FROM RentPayment WHERE RentPayment.TenancyId = Tenancy.TenancyId)
WHERE TenancyId IN (SELECT TenancyId FROM RentPayment);

-- Step 2: Tenancies with only deposit payments (no rent) - set MoveInDate to earliest DepositPayment.PaidOn  
UPDATE Tenancy 
SET MoveInDate = (SELECT MIN(PaidOn) FROM DepositPayment WHERE DepositPayment.TenancyId = Tenancy.TenancyId)
WHERE TenancyId IN (SELECT TenancyId FROM DepositPayment) 
  AND TenancyId NOT IN (SELECT TenancyId FROM RentPayment);
