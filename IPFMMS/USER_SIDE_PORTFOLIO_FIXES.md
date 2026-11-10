# User-side portfolio fixes — local review guide

The user-side gaps identified in the 9 November 2026 portfolio review have been addressed in the source code. The portfolio PDF is unchanged; administration is still deferred. These changes are ready for local testing and user review, not a claim that user acceptance testing is finished.

## What changed

| Portfolio or integration gap | Current behavior |
|---|---|
| Dashboard previews, report p110 | Upcoming unpaid bills within 30 days, overdue bills, and recent financial activities appear alongside the existing financial overview. |
| Filter Bills, report p73 | Scheduled bills support AJAX search by bill name/expense category, type and status, plus Reset, matching count and pagination. |
| Exact original class attributes, Figure 4.24 | All 21 original classes expose all 156 original attribute names; 68 forwarding properties bridge the nine older PascalCase models. |
| Repeat budgets missing until Budget page opened | Dashboard, analytics, insights, financial health and consent-protected monitoring materialize due repeats before using budget data. |
| Monitoring email previously depended on the recipient's browser | Background monitoring checks run at application startup and every five minutes while the process is active, with recipient preferences and consent enforced. |

Dashboard history reads each financial source once. Bill, debt and shared expenses are already normal expense rows and are not counted again from their payment history. Linked records navigate to their appropriate retained history; ordinary income/expense records keep their edit links. Savings allocations and shared releases remain distinct from spending and income.

Bill list filters affect the scheduled list; summary cards continue to describe all of the owner's schedules. Upcoming means future due dates within 30 days, Due today is separate, and Active includes all active dates. Search/filter results remain scoped to the signed-in account. The payment-history filters remain a separate feature. Without JavaScript, the form still submits normally.

## Original attribute names without database renaming

See `CLASS_DIAGRAM_ALIGNMENT.md` for the complete attribute list and compatibility design. The new exact lower-camel properties forward directly to the existing mapped properties and use `[NotMapped, JsonIgnore]`. They do not create extra stored values or SQL columns, and existing names, validation, consumers and JSON payloads remain available.

For both category classes, `status` is the diagram-facing string property over the existing IsActive column: Active means true; Inactive means false. Both directions stay synchronized. Unsupported values are rejected. Continue using the existing mapped PascalCase properties in EF queries; the aliases are an in-memory API, not new queryable columns.

## Repeating budgets across modules

Budget materialization now precedes budget reads in Dashboard, Analytics/report export, Insights, Financial Health/current score/history, monitoring summaries and monitoring alerts. Monitoring locks both accounts in stable order, rechecks the accepted relationship, then applies only the student's existing repeat instructions; it does not let the viewer edit repeat settings.

The shared readiness operation processes successful history in bounded 12-month chunks until the requested month is ready. It stops if no schedule cursor progresses and retains warnings, rather than looping indefinitely over a conflict. Existing active-state, inclusive Repeat Until, series deletion/skipped-month markers and percentage-allocation constraints still apply. Automatic future generation remains limited to 12 months ahead. Existing Edit/Delete preservation calls retain their single-chunk behavior and checks before changing old settings. Calls made inside consent/owner transactions reuse those transactions.

## Background monitoring and email preferences

`MonitoringAlertWorker` runs a scan at application startup, then every five minutes while the app runs. The runner uses stable recipient batches and a fresh service scope per recipient. A failed recipient does not prevent the next recipient from being checked. The service rechecks current consent, account eligibility and duplicate-event keys before generating an alert.

Background monitoring is enabled by default. An explicit server configuration value `MonitoringAlerts:Enabled` of `false` disables background scans. In local user secrets, for example:

```powershell
dotnet user-secrets set "MonitoringAlerts:Enabled" "false" --project .\IPFMMS\IPFMMS.csproj
```

Use the correct project-relative path for your terminal; remove that override or set it to true to enable scans. This global switch is separate from each recipient's Settings. Notification page checks remain available; disabling the worker is not the same as disabling an account's alert channels.

Each recipient controls linked-user alert in-app/email channels independently. Email-only alerts can be delivered without a visible in-app item. Turning email off stops still-pending category emails, and current preferences are rechecked under the same account lock before SMTP. Old in-app history remains subject to the original consent/visibility rules. Disconnection prevents later monitoring access/delivery. Disabled events remain deduplicated and do not become a backlog when settings are re-enabled. Invitation emails follow the recipient's separate invitation preference; security and OTP emails are unaffected.

The recipient does not need an open browser for background monitoring. The application process must be running. A local stopped app or a sleeping free-tier Azure app cannot execute the worker, bill reminders or automatic savings on time; a later active scan checks the eligible state then. This is not an external always-on scheduler. No Azure plan or configuration was changed. Automated checks use fake email senders; real SMTP delivery requires your manual inbox/spam check.

## Database and local startup

These portfolio fixes introduce no further schema migration. EF's `HasPendingModelChanges` check is false. The latest migration remains `20261109101957_AddUserPreferences`, which adds UserPreferences and Notifications.showInApp for the earlier Settings enhancement.

If that migration is not yet applied locally:

1. Stop the app.
2. In Visual Studio, open Tools → NuGet Package Manager → Package Manager Console and select IPFMMS.
3. Run `Update-Database` using your existing migration account if the runtime account cannot alter the schema.
4. Confirm `20261109101957_AddUserPreferences` is applied, then restart the app.

Update-Database also applies any earlier pending migrations; the forwarding attribute aliases themselves need no schema change. Actual local and Azure application databases were untouched by development/validation. No Git commit/push, Azure deployment, cloud changes or admin work was performed. Disposable test databases were used for validation.

## Local manual checklist

1. Open Dashboard with an account containing income, expenses, savings contributions, bill/debt payments and shared entries. Verify totals and the latest ten activities; a linked bill/debt/shared expense should appear once and open the correct history. Sign in as another account and confirm isolation.
2. Create active bills due today, in the next 30 days, overdue, and beyond 30 days. Check the dashboard previews and totals. Mark one paid/inactive and confirm it no longer appears as an unpaid upcoming occurrence. Creating/viewing a bill must not itself record payment.
3. On Routine Bills, type part of a bill name/category, change type/status, combine filters, Reset, and follow pagination. Verify matching/empty states and that the summary cards retain overall counts. Check a narrow mobile view and normal form submission without JavaScript.
4. Set a repeating budget, then use a due month before opening its Budget page. Review Dashboard, Analytics, Insights and Financial Health; they should agree on the same generated limit without manual budget creation. Review an accepted monitoring summary too.
5. Confirm Repeat Until is inclusive, stopping/deleting a series preserves the intended past months, deliberately skipped months stay skipped, and browsing beyond the 12-month future generation horizon does not create extra repeats. Long catch-up and blocked/invalid schedules are covered by automated checks; do not change the real machine clock or hand-edit production data to test them.
6. With accounts A and B, let B accept A's monitoring request. Give B a fresh current-month overspending or low-health event. Keep the local app running with A's browser closed; allow a startup/five-minute scan and check A's notifications/email as configured. Use a fresh event for each test because alerts are deduplicated.
7. Test A's independent linked-alert channels: in-app only, email only, both off. Existing in-app history should remain; disabled old events should not be replayed on re-enable. Disconnect the relationship and confirm further monitoring/delivery is blocked.
8. Confirm theme/account preferences, invitation consent with emails off, and existing security emails still work. Review on desktop and mobile, then record any changes you want next. Actual SMTP inbox delivery and your acceptance review remain manual.

## Validation status

The full application compiles with zero warnings/errors and EF reports no pending model changes. Validation passed: 43 database integration checks, 32 dashboard/filter browser checks, and 8 final mobile-table checks (83 integration/browser checks total). The separate source-linked class check passed 713 exact-name/forwarding/status/JSON compatibility checks. Dashboard and bill-list screenshots were visually inspected in Dark mode. Browser coverage includes AJAX, failure recovery, pagination/Back, account isolation, 320px/390px layouts and no-JavaScript filtering. These checks do not replace local user acceptance testing or a real SMTP inbox test.
