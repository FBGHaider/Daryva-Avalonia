-- Migration 017: Add Tenant, Tenancy, Expense, Document, RentPayment, DepositPayment tables
-- For bulk data import from SQLite

-- ========== TENANTS TABLE ==========
CREATE TABLE IF NOT EXISTS "Tenants" (
    "Id" UUID PRIMARY KEY,
    "OrganizationId" UUID NOT NULL,
    "FullName" VARCHAR(256) NOT NULL,
    "PhoneNumber" VARCHAR(50),
    "Email" VARCHAR(256),
    "UniversityName" VARCHAR(256),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "IsArchived" BOOLEAN NOT NULL DEFAULT FALSE,
    
    CONSTRAINT "FK_Tenants_Organizations" FOREIGN KEY ("OrganizationId") 
        REFERENCES "Organizations"("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Tenants_OrganizationId" ON "Tenants"("OrganizationId");

-- ========== TENANCIES TABLE ==========
CREATE TABLE IF NOT EXISTS "Tenancies" (
    "Id" UUID PRIMARY KEY,
    "OrganizationId" UUID NOT NULL,
    "HouseId" UUID NOT NULL,
    "TenantId" UUID NOT NULL,
    "MoveInDate" TIMESTAMP NOT NULL,
    "MoveOutDate" TIMESTAMP,
    "RentStartMonth" INTEGER,
    "RentStartYear" INTEGER,
    "RentAmountMonthly" DECIMAL(18,2) NOT NULL,
    "DepositAmount" DECIMAL(18,2) NOT NULL,
    "PaymentDueDay" SMALLINT NOT NULL,
    "Status" VARCHAR(50) NOT NULL,
    "Notes" TEXT,
    
    CONSTRAINT "FK_Tenancies_Organizations" FOREIGN KEY ("OrganizationId") 
        REFERENCES "Organizations"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Tenancies_Houses" FOREIGN KEY ("HouseId") 
        REFERENCES "Houses"("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Tenancies_Tenants" FOREIGN KEY ("TenantId") 
        REFERENCES "Tenants"("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_Tenancies_OrganizationId" ON "Tenancies"("OrganizationId");
CREATE INDEX "IX_Tenancies_HouseId" ON "Tenancies"("HouseId");
CREATE INDEX "IX_Tenancies_TenantId" ON "Tenancies"("TenantId");

-- ========== EXPENSES TABLE ==========
CREATE TABLE IF NOT EXISTS "Expenses" (
    "Id" UUID PRIMARY KEY,
    "OrganizationId" UUID NOT NULL,
    "HouseId" UUID NOT NULL,
    "DateIncurred" TIMESTAMP NOT NULL,
    "Category" VARCHAR(100) NOT NULL,
    "Amount" DECIMAL(18,2) NOT NULL,
    "Vendor" VARCHAR(256),
    "Notes" TEXT,
    "ReceiptDocumentId" UUID,
    
    CONSTRAINT "FK_Expenses_Organizations" FOREIGN KEY ("OrganizationId") 
        REFERENCES "Organizations"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Expenses_Houses" FOREIGN KEY ("HouseId") 
        REFERENCES "Houses"("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_Expenses_OrganizationId" ON "Expenses"("OrganizationId");
CREATE INDEX "IX_Expenses_HouseId" ON "Expenses"("HouseId");

-- ========== DOCUMENTS TABLE ==========
CREATE TABLE IF NOT EXISTS "Documents" (
    "Id" UUID PRIMARY KEY,
    "OrganizationId" UUID NOT NULL,
    "TenantId" UUID,
    "TenancyId" UUID,
    "HouseId" UUID,
    "Type" VARCHAR(100) NOT NULL,
    "DisplayName" VARCHAR(256) NOT NULL,
    "FileName" VARCHAR(512) NOT NULL,
    "FileMimeType" VARCHAR(128),
    "StoragePath" VARCHAR(1024),
    "Source" VARCHAR(50),
    "UploadedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "ValidFrom" TIMESTAMP,
    "ValidTo" TIMESTAMP,
    "Version" INTEGER NOT NULL DEFAULT 1,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    
    CONSTRAINT "FK_Documents_Organizations" FOREIGN KEY ("OrganizationId") 
        REFERENCES "Organizations"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Documents_Tenants" FOREIGN KEY ("TenantId") 
        REFERENCES "Tenants"("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Documents_Tenancies" FOREIGN KEY ("TenancyId") 
        REFERENCES "Tenancies"("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Documents_Houses" FOREIGN KEY ("HouseId") 
        REFERENCES "Houses"("Id") ON DELETE SET NULL
);

CREATE INDEX "IX_Documents_OrganizationId" ON "Documents"("OrganizationId");

-- Now add FK constraint for Expenses.ReceiptDocumentId
ALTER TABLE "Expenses"
    ADD CONSTRAINT "FK_Expenses_Documents" FOREIGN KEY ("ReceiptDocumentId") 
        REFERENCES "Documents"("Id") ON DELETE SET NULL;

-- ========== RENT PAYMENTS TABLE ==========
CREATE TABLE IF NOT EXISTS "RentPayments" (
    "Id" UUID PRIMARY KEY,
    "OrganizationId" UUID NOT NULL,
    "TenancyId" UUID NOT NULL,
    "DatePaid" TIMESTAMP NOT NULL,
    "AmountPaid" DECIMAL(18,2) NOT NULL,
    "PaymentMethod" VARCHAR(50) NOT NULL,
    "ReferenceNumber" VARCHAR(100),
    "Notes" TEXT,
    "CollectedBy" VARCHAR(256),
    
    CONSTRAINT "FK_RentPayments_Organizations" FOREIGN KEY ("OrganizationId") 
        REFERENCES "Organizations"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_RentPayments_Tenancies" FOREIGN KEY ("TenancyId") 
        REFERENCES "Tenancies"("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_RentPayments_OrganizationId" ON "RentPayments"("OrganizationId");
CREATE INDEX "IX_RentPayments_TenancyId" ON "RentPayments"("TenancyId");

-- ========== DEPOSIT PAYMENTS TABLE ==========
CREATE TABLE IF NOT EXISTS "DepositPayments" (
    "Id" UUID PRIMARY KEY,
    "OrganizationId" UUID NOT NULL,
    "TenancyId" UUID NOT NULL,
    "DatePaid" TIMESTAMP NOT NULL,
    "AmountPaid" DECIMAL(18,2) NOT NULL,
    "PaymentMethod" VARCHAR(50) NOT NULL,
    "ProtectionScheme" VARCHAR(256),
    "ProtectionReference" VARCHAR(256),
    "Notes" TEXT,
    
    CONSTRAINT "FK_DepositPayments_Organizations" FOREIGN KEY ("OrganizationId") 
        REFERENCES "Organizations"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_DepositPayments_Tenancies" FOREIGN KEY ("TenancyId") 
        REFERENCES "Tenancies"("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_DepositPayments_OrganizationId" ON "DepositPayments"("OrganizationId");
CREATE INDEX "IX_DepositPayments_TenancyId" ON "DepositPayments"("TenancyId");
