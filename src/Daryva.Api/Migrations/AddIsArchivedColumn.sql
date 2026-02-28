-- One-time fix for production: add IsArchived to Houses and record the migration.
-- Run this inside the postgres container against your daryva database.
--
-- Example (from VPS, adjust container name and db user if needed):
--   docker exec -i daryva-postgres-prod psql -U daryva -d daryva < AddIsArchivedColumn.sql
-- Or paste the lines below into an interactive psql session.

-- Add column (safe if already exists in PostgreSQL 9.5+)
ALTER TABLE "Houses" ADD COLUMN IF NOT EXISTS "IsArchived" boolean NOT NULL DEFAULT false;

-- Tell EF Core this migration was applied (so it won't try to run it again)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260226230000_AddHouseIsArchived', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;
