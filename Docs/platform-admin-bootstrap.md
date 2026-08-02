# Platform admin bootstrap

`AppUser.IsPlatformAdmin` marks a Daryva staff account. It is **not** an `OrganizationMember`
row and grants no access to any landlord's org data by default -- an admin only gets into a
specific org's data through an explicit, time-boxed, logged Support Session (see the
Support Mode design; not yet implemented).

## Granting platform admin

Set `Admin:BootstrapEmails` (comma-separated) in config -- environment variable
`Admin__BootstrapEmails`, or the relevant `appsettings.*.json` / secrets store per environment.
On every API startup, any `AppUser` matching one of those emails gets `IsPlatformAdmin = true`
if they don't already have it. The account must already exist (register normally first) --
bootstrap only flips the flag, it doesn't create the user.

This is idempotent and additive only:
- Safe to leave the config value in place indefinitely; it won't re-grant or change anything
  once a user already has the flag.
- Clearing the config does **not** revoke admin from anyone already granted -- there is no
  "de-admin via config" path by design, since a config typo shouldn't be able to silently
  remove someone's access.

Each grant is recorded in `AuditLogs` (`EventType = PlatformAdminGranted`, actor `System`).

## Revoking platform admin

No API endpoint for this yet. Until one exists, flip the flag directly:

```sql
UPDATE "AppUsers" SET "IsPlatformAdmin" = false WHERE "Email" = 'someone@example.com';
```
