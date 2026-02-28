# EF Core migrations

- **Full guide:** See [../MIGRATIONS.md](../MIGRATIONS.md) for safe schema changes and deploy order.

**Before adding a new migration:**

1. New column → use **nullable** or **NOT NULL + defaultValue** so the current API keeps working.
2. Avoid **NOT NULL** without a default unless you use a two-phase approach (nullable first, then NOT NULL later).
3. Drops/renames only after no running code uses the old schema.

Then run:

```bash
dotnet ef migrations add YourMigrationName --project src/Daryva.Api/Daryva.Api.csproj
```

Review the generated `Up()` in the new migration file against the guide before committing.
