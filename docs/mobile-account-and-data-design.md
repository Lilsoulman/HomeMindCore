# Mobile Account and Data Design

## Decision

This reconciles the original mobile plan with
`D:\HomeMind\mobile\docs\BACKEND_DESIGN.md`. The executable baseline is the
MySQL 8 / SqlSugar migration
[001_mobile_initial_schema.mysql.sql](../database/001_mobile_initial_schema.mysql.sql).

The frontend document is the source of truth for the existing business route
names and DTO field names. This document is the source of truth for identity,
session, WeChat and sync security. The service runs as the existing
`HomeMind.Api` .NET 8 Web API with SqlSugar and MySQL 8; do not introduce a
second API process or an EF Core / SQL Server variant.

`users` is the person and owns business data. `user_identities` is every
verified way that person can sign in. A WeChat identity, verified phone,
verified email, and password can therefore belong to the same user. Do not put
an `openid`, `unionid`, phone number, email address, or password hash directly
in `users`.

## Account modes

| User-facing mode | Server records | Result |
| --- | --- | --- |
| Local / guest | No server user or token | Data remains local; import it only after a real sign-in. |
| WeChat | `users`, WeChat `user_identities`, device, refresh-token session | First verified login creates an account; later logins enter the same account. |
| Phone SMS | `users`, verified `phone/e164` identity, device, session | Passwordless sign-in and a fallback identity for a WeChat account. |
| Phone/email + password | Verified phone/email identity plus `password_credentials` | Password is optional credential, not an account identifier. |
| Existing account binding | New identity after recent authentication | One person can sign in with WeChat or phone/password. |

Keep guest mode local-only in the H5 release. A browser installation ID is not
an account proof. If cloud guest sync is later required, use a recovery secret
stored in native secure storage, not browser local storage.

## WeChat one-tap login

The current client is H5, so "one-tap login" depends on the runtime:

| Runtime | Client obtains | Backend exchange |
| --- | --- | --- |
| WeChat browser H5 | OAuth authorization `code` after redirect | Official Account OAuth credentials |
| Android/iOS wrapper | WeChat Open SDK authorization `code` | Open Platform AppId and AppSecret |
| Mini Program, if added | `wx.login()` `code` | Mini Program AppId and AppSecret |

The client submits only `{ code, channel, installationId }` to
`POST /api/v1/auth/wechat/exchange`. The backend validates `channel`, exchanges
the code with WeChat and creates or finds the identity. WeChat tokens,
AppSecrets, raw profile responses and identifiers must never be returned to or
persisted by the client.

Matching rules:

1. When present, use `unionid` with `issuer=wechat-open-platform` as the
   cross-application identity.
2. Retain AppId-scoped `openid` as a verified secondary identity.
3. Without a union ID, match only `(wechat, AppId, openid)`; do not infer that
   IDs from different AppIds belong to the same person.
4. A union ID belonging to another account returns `IDENTITY_CONFLICT`. Require
   both accounts to authenticate, then use an explicit merge flow. Never move
   an identity or merge personal data automatically.

Before production, register each relevant WeChat product and configure the H5
OAuth callback domain, iOS Universal Link, Android package/signature, and
server-side secret storage. Product eligibility differs by WeChat channel, so
the AppId is configured by the server and is never supplied by the client.

## Authentication contract

| Endpoint | Purpose |
| --- | --- |
| `POST /auth/challenges` | Rate-limited SMS/email verification challenge |
| `POST /auth/phone/exchange` | Consume SMS challenge to register or sign in |
| `POST /auth/password/login` | Verified phone/email plus password |
| `POST /auth/password` | Set/change password after recent authentication |
| `POST /auth/identities/bind` | Bind WeChat, phone, or email |
| `DELETE /auth/identities/{id}` | Unbind only when another verified login method remains |
| `POST /auth/refresh` | Rotate opaque refresh token |
| `POST /auth/logout` | Revoke current token family |
| `GET /auth/sessions`, `DELETE /auth/sessions/{deviceId}` | Manage device sessions |
| `POST /auth/merge/prepare`, `POST /auth/merge/confirm` | Explicitly merge authenticated accounts |

For compatibility with the frontend contract, `POST /auth/register` remains
the phone/password registration endpoint and `POST /auth/login` remains the
phone-or-email/password endpoint. Registration must additionally include a
consumed phone/email verification challenge before a password credential is
created. Existing business routes from the frontend document, including
`/todos`, `/calendar/*`, `/skills`, `/ai/*`, `/attachments`, `/weather`, and
`/push/*`, are retained unchanged under `/api/v1`.

Use a 15-minute JWT access token and an opaque refresh token (30 days unless
product policy says otherwise). Persist only `SHA-256(token + server pepper)`.
Rotate on each refresh; a reused revoked token revokes its full `family_id`.
For H5, access tokens stay in memory and refresh tokens use Secure, HttpOnly,
SameSite cookies. Native wrappers use secure OS storage.

Passwords use Argon2id with a unique salt and approved parameters. Verification
codes are stored hashed, expire quickly and have a small attempt limit. Rate
limit login, SMS/email sending and code attempts by subject, IP and device.
Identity bind/unbind, password change, session revocation, export and deletion
require recent authentication. Audit all security events without storing raw
codes, passwords, tokens, phone numbers or WeChat IDs.

## Data mapping

| Area | Tables | Notes |
| --- | --- | --- |
| Account/security | `users`, `user_identities`, `password_credentials`, `auth_devices`, `auth_refresh_tokens`, `auth_verification_challenges`, `user_consents`, `auth_audit_logs` | Personal profile and credentials are deliberately separate. |
| Todo | `todo_lists`, `todos`, `todo_tags`, `todo_tag_links`, `attachments` | Supports subtasks, RRULE recurrence, generated repeats, filters and attachments. |
| Calendar | `calendar_events`, `calendar_event_exceptions`, `calendar_subscriptions` | Exceptions preserve edits/cancellations of RRULE occurrences; subscription URLs are encrypted. |
| AI/settings | `ai_skills`, `ai_configs`, `ai_call_logs`, `user_settings` | API keys are encrypted and excluded from sync payloads. |
| Push | `push_subscriptions` | Endpoint and keys are encrypted; each belongs to a device. |
| Sync | `sync_clients`, `sync_mutations`, `sync_change_log` | Supports idempotent offline push and cursor-based pull. |
| Expert workbench | `experts`, `expert_versions`, `expert_groups`, `expert_group_versions`, `expert_runs`, `run_steps`, `run_events`, `run_artifacts`, `credit_ledger` | M6 catalog, immutable execution snapshots, queue-backed execution and result traceability. |

Attachments store metadata only. File bytes belong in the planned mounted upload
volume or object storage, never `VARBINARY(MAX)`. Enforce content type and size
server-side and clean orphan files asynchronously.

## Offline sync

Every mutable business record has `updated_at`, `deleted_at`, and
`sync_version`. Deletes are tombstones during the retention window. In the same
transaction as every mutation, the service inserts a `sync_change_log` row and
uses its `AUTO_INCREMENT sync_version` as the cursor written to the entity.

1. Client writes locally and assigns a UUID `mutationId`.
2. `POST /api/v1/sync/push` sends mutations with `installationId`.
3. Server records `(clientId, mutationId)` so retries are idempotent.
4. Server applies LWW with client time clamped to allowed clock skew and
   `clientId` as deterministic tie-breaker. Account/security changes never use LWW.
5. `POST /api/v1/sync/pull` accepts the legacy `{ entity, since }` shape while
   the frontend is migrating; its response also includes `nextCursor`. The
   canonical `GET /api/v1/sync/pull?cursor=` reads the change log in cursor order and
   returns current rows, tombstones and `nextCursor`.

Authorize every row by server-derived `user_id`; do not accept a client supplied
`userId`. A plain `?since=<timestamp>` protocol is insufficient because it
cannot reliably represent deletes, retries or clock skew.

## Mobile plan updates

- Replace M4's phone/password-only page with WeChat-first entry, phone SMS and
  password fallback. Creating a password requires verified phone or email.
- Add identity binding, device-session management, explicit account merge and
  local guest-data import to the Me-page scope.
- Replace the M5 `users` DDL draft with this migration. `password_hash` is not
  required for every user, and WeChat identifiers are never in `users`.
- Replace timestamp-only sync with the mutation/cursor protocol above.

## Implementation delta for the frontend backend document

The current frontend document's `users.phone/email/password_hash` and
`refresh_tokens` tables must be replaced by `user_identities`,
`password_credentials`, `auth_devices`, and `auth_refresh_tokens`. Its simple
`sync_records` timestamp watermark is retained only as a temporary client
compatibility aid and is not sufficient as the server conflict source.

Add the following SqlSugar entities and services before implementing the auth
controller: `UserIdentity`, `PasswordCredential`, `AuthDevice`,
`AuthRefreshToken`, `AuthVerificationChallenge`, `AuthAuditLog`, `SyncClient`,
`SyncMutation`, and `SyncChangeLog`. All existing `long` IDs remain valid;
offline retry uses a separate UUID `mutationId`, so the front end does not need
to generate database IDs.

## Expert workbench and PDMANER

M6 extends the model through
[002_expert_workbench_and_tenancy.mysql.sql](../database/002_expert_workbench_and_tenancy.mysql.sql).
It introduces `tenants` and `tenant_members` now, while the product is still
personal-first: registration creates a personal tenant and owner membership;
the JWT supplies the active tenant. This protects future shared workspaces
without changing existing mobile resource IDs or routes.

Expert catalog configuration is versioned and immutable at execution time.
Runs reference exactly one expert or group version, retain user-approved input
context, and persist only safe progress summaries and structured output. The
worker dequeues `expert_jobs`; model usage and charges are recorded separately
from the run so cancel/retry cannot duplicate cost entries. `expert_run_actions`
makes a result-to-plan/todo/calendar conversion idempotent and stores its
source reference on created records.

Use [the PDMANER import guide](../database/pdmaner/README.md) to apply both
MySQL migrations to a disposable schema and reverse-engineer the resulting
physical model into six diagram areas. Keep the SQL migrations as the
version-controlled source of truth; PDMANER is the reviewed visual model and
DDL generation surface. The tenancy migration is staged (nullable tenant
columns, backfill, then foreign keys), so it is suitable for existing M5 data
as well as a new environment.
