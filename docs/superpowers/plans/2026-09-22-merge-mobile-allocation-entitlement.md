# Merge mobile allocation and entitlement

## Outcome

A mobile allocation is created and edited with mobile number, employee EPF selection, monthly credit limit, and monthly rental. `MobileAccounts` owns all four values. Existing `MonthlyBills` retain their amount snapshots. The separate entitlement screen and API are removed.

## Tasks

1. Add failing API and UI tests for the combined create flow, amount validation, and response values.
2. Move the amount fields into the domain entity, DTOs, EF configuration, and master data service; remove separate entitlement CRUD.
3. Update matching and historical exception resolution to use allocation amounts; retain assessed bill monetary snapshots.
4. Update the React master-data form and list, navigation, and affected tests.
5. Generate a SQL Server migration that backfills only unambiguous existing entitlement data, blocks ambiguous or missing source rows, then removes the old table. Apply it to the local database after verifying row counts.
6. Run .NET build/tests, frontend build/tests, and read-only SQL schema checks. Report any remaining failures.

## Data safety

The migration must refuse to discard multiple historical entitlements for one account. It must preserve bill-line and assessed-bill records. The current local database has no allocations or entitlements, so the schema change does not delete allocation data there.
