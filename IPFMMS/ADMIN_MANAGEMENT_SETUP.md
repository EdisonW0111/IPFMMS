# Administration management — local setup and review

The Administration module implements the portfolio's User Account Management, Parent–Student Relationship Management, Income Category Management, Expense Category Management, and System Dashboard functions (Table 3.4, report p53; detailed use cases pp71–72; account-management flow pp88–89; Admin Dashboard interface p123). Original Chapter 4 classes/attributes remain available; the supporting audit entity is an allowed addition.

The user-side consent model remains in force: every ordinary user can request monitoring of zero or more other users, including users who are themselves monitored. Administrators manage accounts, categories and relationship state without granting themselves access to a person's financial transactions or bypassing recipient acceptance.

## Access and first administrator

All management pages require a signed-in, active Admin account. Services recheck the current database role/status, rather than relying only on an old login cookie. Mutations lock the acting administrator together with all affected accounts in one global UserId order, recheck active Admin access while those locks are held, and reject stale confirmation/edit stamps. Category changes then lock the selected category; read-committed usage checks avoid locking financial rows, while foreign-key restrictions still guard deletion. The actor identity comes from the authenticated session, never a posted form user ID.

To create the first local administrator with exactly `UID00000`, use the separate interactive command documented in [tools/CreateFirstAdmin/README.md](../tools/CreateFirstAdmin/README.md). Run it after applying migrations. It prompts privately for a new password, creates the Admin account only when explicitly executed by the local owner, refuses existing IDs/emails/active Admins, and leaves normal UID numbering unchanged. It never runs at web-app startup and rejects remote/Azure databases.

No real administrator was seeded or created during development. The user will create/prepare an administrator account later through the agreed setup process. Public registration remains an ordinary User registration flow. This guide does not promote an existing account or change real credentials. Automated checks use a disposable fake administrator.

Administrators can open their own existing profile and password pages from the account menu. Their own Settings page manages appearance only (Light/Dark); ordinary-user financial notification controls are not shown there. These routes still use the signed-in account identity and do not let an administrator change someone else's password through the management UI.

## Account management

Administrators can search/filter/paginate accounts, review account details and record counts, edit name/phone, assign User or Admin, and change verified accounts between Active and Inactive. A legacy Parent role can be retained for existing accounts, while ordinary users continue to use parental monitoring without a separate parent account.

- Email is read-only in management forms; it is not changed outside the established verification flow.
- Pending accounts must complete email verification before activation or promotion. Their name/phone may be corrected while they remain Pending in the same role.
- An administrator cannot disable, delete or demote their own administrator account; this check uses the loaded canonical account ID, including when a route uses different letter casing.
- Role/status changes increment SecurityVersion and invalidate unused password-reset authorization tokens. Existing sessions lose their old access when checked again.
- Narrow account/OTP/reset-token locking serializes email verification and password writes with administrative access revocation. Each write rechecks the current account state while locked; an old Pending verification/reset request cannot revive a Deleted account or silently undo a newer access change.
- Delete Account is a separate confirmation. It sets AccountStatus to Deleted, revokes access, and retains account/financial history; it does not cascade-delete transactions, goals, debts, shared entries or relationships. Deleted is terminal/read-only through management.
- Passwords, PasswordHash and reset tokens are not displayed or editable in account management. Administrators do not set someone else's password through these pages.

Per-account financial counts indicate that records exist; management does not expose individual financial amounts, transaction descriptions, receipts or the user's personal finance dashboard.

## Income and expense categories

Categories have separate Income/Expense lists with search, Active/Inactive filtering, pagination and DisplayOrder sorting. Create/Edit validates a trimmed name of 1–100 characters, description up to 255 characters, nonnegative DisplayOrder and active state. Names must be unique within their category type, with case/whitespace-aware duplicate checks and a friendly database-conflict message. Changes retain category IDs and existing financial references.

Deletion is allowed only for an unused, unprotected category. Income usage includes Income rows. Expense usage includes Expenses, Budgets, BudgetSchedules, RoutineBills and SharedFinanceTransactions, including retained/inactive history. The confirmation shows usage and offers deactivation instead of removing referenced data. Server checks and database restrictions also enforce this policy; posting a different display count cannot bypass it. No category operation changes a financial amount or deletes related financial rows.

The Expense categories named Debt Repayment and Shared Savings are system identities used by financial services. Their names stay unchanged and they cannot be physically deleted, even if unused. Description, display order and active state remain editable. Deactivation stops new operations requiring an active system category, such as new debt repayments/shared savings contributions. Existing payment/contribution history is retained; releasing an existing shared savings allocation remains possible under its existing rules.

The original diagram-facing category.status property still forwards Active/Inactive to the existing IsActive database field. No second status column or unsynchronized status value is introduced. See CLASS_DIAGRAM_ALIGNMENT.md for the exact attribute compatibility design.

Existing category-seeder values are preserved, including an administrator-selected DisplayOrder of 1000 and existing active state. New default categories are added only when the existing manual seeding operation is intentionally run; these management pages do not overwrite custom category values or automatically reseed the database.

## Relationship management and consent

Administrators can list/search/filter relationships, initiate a Pending monitoring request between eligible ordinary users, cancel a pending request, and deactivate a current relationship. Creating or retrying a request respects the recipient's invitation-email preference; an email being disabled/failed does not grant access or remove the recipient's in-app review.

The recipient accepts or rejects through their own account. Management cannot accept on the recipient's behalf, turn a rejected/inactive link straight into monitoring access, or reactivate an old consent version. Reconnecting requires a fresh Pending request and fresh acceptance. Cancellation/deactivation retains relationship/audit history and ends further authorized monitoring/delivery under the existing guards.

An old row whose linkStatus says Active but has no acceptedDate is shown as unavailable. It is not treated as valid consent; a fresh request/acceptance is required. Administrative relationship actions do not display the linked user's financial details.

## System dashboard and management history

The Admin Dashboard presents aggregate account/category/relationship and financial-record counts, plus recent management history. It provides navigation to management pages, not a way to browse an individual's finances.

Each successful account/category/relationship mutation adds an AdminAuditEntry in the same transaction as the change. The entry records administrator, target type/ID, action, concise summary and UTC timestamp. Failed/stale/unauthorized requests do not commit management changes or an audit entry for a change that did not happen. Management history is read-only in the application; no edit/delete-audit endpoint is provided.

## Migration and local startup

The exact new migration is `20261109113958_AddAdminManagement`. It adds only AdminAuditEntries and its supporting relationships/indexes; it creates no administrator account or seed data. It does not rename/remove the original portfolio entities or alter financial records.

If the new audit migration has not yet been applied locally:

1. Stop the app.
2. Open Visual Studio → Tools → NuGet Package Manager → Package Manager Console and select IPFMMS.
3. Run Update-Database with the existing local migration account if the runtime account cannot alter schema.
4. Verify the latest applied migration is `20261109113958_AddAdminManagement`, then restart.

Earlier unapplied migrations are applied as part of Update-Database too. Development did not apply this upgrade to the actual local or Azure application database. No Azure deployment/configuration, Git commit/push, real administrator creation, credential change or real SMTP action was performed. Automated integration uses isolated databases and fake email senders.

## Manual checklist — after the administrator account is prepared later

1. Sign in as the prepared active administrator; verify Dashboard aggregates, management links, and access to your own profile/password pages. Open your Settings and confirm it offers appearance only; save Light/Dark mode. Sign in as an ordinary user in another browser and confirm management pages/actions are denied.
2. Search/filter accounts and open details. Confirm email is read-only, passwords/hashes/tokens are absent, and only financial record counts are shown.
3. Change a verified test user's name/phone and status/role. Verify a second, already-signed-in session loses revoked access and an old password-reset authorization no longer works. Restore the test account deliberately as appropriate.
4. Try activating/promoting a Pending test account before email verification. Expect rejection. Try self-disable/self-demotion/self-delete as the administrator. Expect rejection. In two tabs, save a newer edit, then submit the old edit; expect a reload message.
5. Delete a disposable test account with confirmation. Confirm sign-in is blocked, management shows Deleted/read-only, and its existing financial records remain.
6. Create/edit Income and Expense categories; check search/status filters and DisplayOrder. Reject duplicate names differing only by case/outer spaces. Delete a truly unused test category. Attempt deletion of a category used by each relevant module; expect retention/deactivation guidance.
7. Try renaming/deleting Debt Repayment or Shared Savings; expect rejection. Test description/order changes on disposable data. If testing deactivation, verify new dependent operations are blocked, old history remains, and existing shared allocations can still be released; restore the category afterward.
8. Initiate a relationship between two consenting test users as Pending. Confirm the recipient must accept themselves before monitoring is available. Cancel a pending request or deactivate an accepted link; verify no direct reactivation/acceptance bypass and fresh consent is needed for reconnection.
9. Check an old Active row without acceptedDate remains unavailable. Verify recipient email-off keeps requests reviewable without sending email. Real mailbox delivery is a separate manual check after normal SMTP setup.
10. Review management history after each successful action. Confirm failed/stale operations do not leave partial changes, and audit rows have no edit/delete actions. Check desktop/mobile layout and record any changes desired after this local review.

## Validation scope

The final product build passed with zero warnings and zero errors. The migration matched the EF model. All 74 backend integration checks passed (67 schema/permissions/workflow checks and 7 additional authentication/seeder checks), including canonical-ID protection, cookie revocation, deleted-account OTP verification, category-seeder preservation and concurrent administrator changes.

The browser suite passed 212 assertions covering account/category CRUD, stale forms, access restrictions, consent, AJAX filtering and ordinary GET fallback. After correcting the shared layout's missing jQuery dependency, 78 focused checks passed with zero JavaScript exceptions, including initialized form validation, dashboard/navigation, Admin profile/password access, appearance settings, and 320px/390px layouts. Desktop and mobile screenshots were reviewed.

Tests used disposable databases, temporary administrator fixtures and fake email senders. They do not substitute for the user's later acceptance review or real SMTP inbox tests. The actual application database and accounts were not changed, and nothing was committed, pushed or deployed.
