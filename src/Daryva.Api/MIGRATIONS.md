# Safe schema changes (Daryva API)

This guide keeps production stable when you add or change database schema. The goal: **deployments never break the running API** because of missing columns or incompatible schema.

---

## 1. Deploy order (already in place)

The GitHub Actions workflow **runs migrations before starting the new API**:

1. Build and push **migrate** image (same code as API, runs `dotnet ef database update`).
2. On the VPS: **run migrate container** → applies pending migrations.
3. Then **pull and start the new API** → new code runs against updated schema.

So the database is always updated before the new API serves traffic. No “column does not exist” from new code hitting old schema.

---

## 2. Backward-compatible migrations (expand–contract)

When adding or changing columns, design migrations so that **the current (old) API still works** after the migration runs. Then deploy the new API in the next step.

### Adding a new column

| Goal | Safe approach | Avoid |
|------|----------------|--------|
| New optional field | Add column as **nullable** (`nullable: true`). Old code ignores it; new code can use it. | Adding `NOT NULL` without a default in the same release. |
| New required field with a sensible default | Add column as **NOT NULL** with **`defaultValue`** (or **`defaultValueSql`**). Old code never reads it; new code and DB both have a value. | `NOT NULL` with no default (old code might not write it). |
| New required field with no good default | **Phase 1:** Add column as **nullable**. Deploy migration + new code that writes the column. **Phase 2:** Later migration adds `NOT NULL` (and optionally backfill + constraint). | Adding `NOT NULL` in the first migration when the old API doesn’t know about the column. |

**Example – new optional column (nullable):**

```csharp
migrationBuilder.AddColumn<string>(
    name: "Notes",
    table: "Houses",
    type: "character varying(500)",
    maxLength: 500,
    nullable: true);  // old API ignores it
```

**Example – new required column with default (safe in one step):**

```csharp
migrationBuilder.AddColumn<bool>(
    name: "IsArchived",
    table: "Houses",
    type: "boolean",
    nullable: false,
    defaultValue: false);  // old API never reads it; new API and DB agree
```

**Example – new required column, no default (two-phase):**

```csharp
// Phase 1 migration: add nullable
migrationBuilder.AddColumn<DateTime>(
    name: "EffectiveFrom",
    table: "Contracts",
    type: "timestamp with time zone",
    nullable: true);

// Phase 2 migration (later release): backfill, then make NOT NULL
// migrationBuilder.Sql("UPDATE \"Contracts\" SET \"EffectiveFrom\" = \"CreatedAt\" WHERE \"EffectiveFrom\" IS NULL");
// migrationBuilder.AlterColumn<DateTime>(... nullable: false);
```

### Changing or removing columns

- **Rename column:** Add a new column, deploy code that writes both (or only the new one), then drop the old column in a later migration.
- **Remove column:** Stop using it in code first (deploy), then drop it in a later migration.
- **Change type/default:** Prefer a new column + backfill + switch code + drop old column over in-place change if the old API still reads the column.

---

## 3. Checklist before adding a migration

- [ ] **New column:** Is it nullable or does it have a default so the current API doesn’t break?
- [ ] **NOT NULL without default:** If yes, use a two-phase approach (add nullable first, then NOT NULL in a later release).
- [ ] **Drop/rename:** Only after no running code still uses the old column/name.
- [ ] **Deploy:** Rely on the workflow to run **migrate** before **api**; don’t skip the migrate step.

---

## 4. Creating a new migration locally

```bash
cd src/Daryva.Api
dotnet ef migrations add YourMigrationName
```

Then review the generated `Up` method against this guide and the checklist above. Adjust if needed (e.g. make a column nullable or add a default) before committing.

---

## 5. Running migrations

- **Production:** Handled by the deploy workflow (migrate image runs, then API starts).
- **Local:** Either start the API (it runs `MigrateAsync()` on startup) or run:
  ```bash
  dotnet ef database update --project src/Daryva.Api/Daryva.Api.csproj
  ```
