# PDMANER Import Guide

## Source of truth

Use the versioned MySQL migrations as the source of truth:

1. `../001_mobile_initial_schema.mysql.sql`
2. `../002_expert_workbench_and_tenancy.mysql.sql`

Use the PDMANER MySQL 8 dialect with `utf8mb4` / `utf8mb4_unicode_ci` and UTC
`DATETIME(3)`. SQL remains the authoritative, reviewable format; a PDMANER
model is the visual development artifact and must be regenerated from it.

## Recommended PDMANER workflow

1. Create a disposable local MySQL schema (the local development migration
   targets `nexus_mind`), then run `001` followed by `002`.
   `002` includes the personal-tenant bootstrap and is intentionally safe for
   an empty database or existing baseline data.
2. In PDMANER 4.9.2, create/open a MySQL physical model and use its database
   reverse-engineering/import-database feature against that schema. This
   brings in primary keys, unique keys, indexes and foreign keys from MySQL.
3. Save the generated native model alongside the migrations as
   `database/pdmaner/homemind.pdma.json`. The root-level
   `Nexus_Mind.pdma.json` currently contains only PDMANER defaults and no
   entities, so it must not be used as the completed database model.
4. Arrange the imported tables into the six areas below. Before a release,
   review the PDMANER DDL diff and add approved changes as `003_*.sql` (or the
   next number). Do not overwrite `001` or `002` after either has been applied.

If a live MySQL instance is unavailable, PDMANER may import the raw scripts
in order. Re-open the generated model and compare its DDL with both migration
files, because SQL-script importers can omit `ALTER TABLE` changes.

## Areas to lay out

Create six diagram areas after import:

| Area | Tables |
| --- | --- |
| Identity and tenancy | `users`, `user_identities`, `password_credentials`, `auth_*`, `tenants`, `tenant_members` |
| Personal productivity | `todo_*`, `todos`, `subtasks`, `attachments`, `plans`, `plan_items` |
| Calendar | `calendar_*`, `ical_overrides` |
| AI and configuration | `ai_*`, `user_settings`, `push_subscriptions` |
| Synchronisation | `sync_clients`, `sync_mutations`, `sync_change_log` |
| Expert workbench | `experts`, `expert_versions`, `expert_groups`, `expert_group_versions`, `expert_group_members`, `expert_runs`, `run_*`, `expert_jobs`, `credit_ledger`, `expert_run_actions` |

## Development rules

- PDMANER's generated model must preserve table, column, index and constraint
  names from the SQL. Generate DDL only through review and add it as the next
  numbered migration; never hand-edit production schema in PDMANER.
- `users` is global identity. The server resolves `tenant_id` from the JWT and
  membership; clients must never submit an authoritative tenant ID.
- Built-in experts are owned by seeded system tenant `id=1`; each registered
  user receives a personal tenant and owner membership.
- `row_version` implements optimistic concurrency in SqlSugar for mutable
  catalog/run rows. `sync_version` plus `sync_change_log` is only for mobile
  data synchronisation.
- Do not model raw model chain-of-thought. Store only user-safe summaries,
  structured output, tool summaries, error codes and usage records.
