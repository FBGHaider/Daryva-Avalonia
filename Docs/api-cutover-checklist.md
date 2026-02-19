# API Cutover Checklist (Financial-Safe)

## Goal
Switch from local SQLite mode to full API mode **only when parity is exact** for all financial and operational data.

## 1) Freeze changes
- Stop any new payment/tenant/expense edits while validation is running.
- Keep current mode on `Local` during migration and verification.

## 2) Verify source database
- Confirm source SQLite path is the authoritative DB with full history.
- Example sanity checks:
  - Houses, tenants, tenancies counts
  - Rent payments + deposit payments counts and total amounts
  - Expenses/documents counts

## 3) Migrate to API org
- Run migration using the correct source DB.
- Prefer a clean/empty target org for deterministic results.

## 4) Run parity gate (must pass)
- Run:
  - `powershell -ExecutionPolicy Bypass -File .\Scripts\verify-api-cutover.ps1 -OrgId <TARGET_ORG_ID>`
- Required status:
  - `Cutover status: SAFE (all metrics match)`
- If blocked:
  - Do **not** switch mode.
  - Investigate mismatched metrics and re-run migration.

## 5) Switch mode using one flag
- Set Local mode:
  - `powershell -ExecutionPolicy Bypass -File .\Scripts\set-data-mode.ps1 -Mode Local -Restart`
- Set API mode:
  - `powershell -ExecutionPolicy Bypass -File .\Scripts\set-data-mode.ps1 -Mode Api -Restart`

## 6) Post-switch smoke checks (mandatory)
- Open rent ledger, deposit ledger, transactions.
- Validate key tenants/properties for:
  - same balances
  - same payment history ordering
  - no extra/unusual payments
- Record one test payment and verify it appears consistently in all relevant views.

## 7) Rollback plan
- If any mismatch appears after switch:
  - Immediately set mode back to Local:
    - `powershell -ExecutionPolicy Bypass -File .\Scripts\set-data-mode.ps1 -Mode Local -Restart`
  - Re-open parity investigation before retrying API mode.
