# Settings — notifications and appearance

Settings is now a working user-side page in the sidebar. It adds account preferences without renaming any existing Chapter 4 class or attribute. The portfolio review is recorded separately in `D:\Downloads\IPFMMS_User_Side_Review_2026-11-09.md`; the Settings enhancement and the subsequent user-side portfolio fixes are documented in `USER_SIDE_PORTFOLIO_FIXES.md`. The audit findings are now marked with their resolution status.

## Local setup

1. Stop the app.
2. Open Visual Studio → Tools → NuGet Package Manager → Package Manager Console; select IPFMMS.
3. Run `Update-Database`.
4. Verify the latest migration is `20261109101957_AddUserPreferences`.
5. Restart and open Settings.

The migration adds `UserPreferences` (one row per user) and `Notifications.showInApp` (default true). Existing notifications remain visible under the existing ownership/consent checks. Accounts with no saved preferences default to Light mode with all existing alert channels enabled. Earlier unapplied migrations are also applied by Update-Database; use the existing migration account if the runtime account cannot alter schemas.

No actual local application database or Azure database was changed during this implementation. Tests use a disposable database on a separate port. Nothing was committed, pushed or deployed.

## Notification preferences

| Category | In-app | Email |
|---|---|---|
| Bill reminders: upcoming, due today, overdue | Optional | Optional |
| Linked-user alerts: budget overspending and low Financial Health | Optional | Optional |
| Monitoring requests and shared-finance invitations | Pending requests always remain on the relevant pages | Optional |

Preferences belong to the recipient, not the person sending the request. Ordinary account users can save only their own preferences. OTP, verification and password-reset emails are unaffected; authentication email delivery cannot be disabled here.

- Changes affect new alerts. Existing in-app history stays available, subject to the existing consent/bill-validity rules.
- Email-only alerts are sent without creating a visible in-app item. In-app-only alerts do not send email.
- Turning email off also disables matching messages still waiting to be sent. Senders recheck the current preference while holding the same account lock used by Settings.
- Skipped/disabled alerts retain an event marker for duplicate prevention. Turning a category back on does not send a backlog or recreate the same old event. New eligible events use the new preference.
- Disabling invitation emails does not reject, hide or automatically accept a request. The recipient can sign in and accept/reject normally. The sender sees that email is disabled, not a false Sent status or an SMTP error.
- Existing application-wide switches still apply. For example, enabling bill emails in Settings does not turn on a disabled `RoutineBills:RemindersEnabled` server feature.

Monitoring alerts now also run through MonitoringAlertWorker at startup and every five minutes while the application process runs, so the recipient does not need an open browser. Scans use stable batches and fresh recipient scopes, recheck accepted consent/account eligibility, and respect the recipient's in-app/email preferences, pending-email opt-out and event deduplication. Background scans are enabled by default; an explicit MonitoringAlerts:Enabled=false server setting disables the worker. Notification page checks remain available. Bill reminders retain their existing worker/page checks and RoutineBills:RemindersEnabled switch. A stopped or sleeping app cannot execute background checks on time; no external always-on scheduler was introduced. No real SMTP configuration was changed, and automated tests use fake senders; real inbox/spam delivery remains a manual check.

## Appearance

- Choose Light or Dark; selecting a card previews the theme immediately.
- Save preferences to persist it to the account. A new browser/device reads that same account value after login.
- An unsaved preview is discarded on reload/navigation. A different account uses its own saved preference, even if the browser previously displayed another user's Dark mode.
- A non-sensitive theme cookie carries the last saved appearance to public/account screens such as Login. Its value is validated; signed-in account preferences take precedence.
- Shared layouts, standalone authentication pages, forms, tables, alerts, menus, status text and analytics SVG charts have dark styling. Print output remains light; CSV and PDF financial calculations are unchanged.
- The server renders the initial theme; saving remains functional when JavaScript is unavailable. JavaScript supplies only live preview and chart redraw.

## Manual test checklist

1. Save Dark mode, navigate through Transactions/Budgets/Savings/Analytics/Debt/Shared Finance, then reload. Check backgrounds, text, form fields, dropdowns and charts.
2. Sign in as the same account in another browser; confirm the saved mode appears. Sign in as a different account with no saved mode; confirm it starts Light.
3. Check Settings at desktop, 390px and 320px mobile widths, and print preview. Switch back to Light and save.
4. Turn off bill email but keep in-app on. Create a fresh eligible bill event, then trigger its reminder check. Confirm an in-app reminder appears with email disabled. Reuse a different event for later checks because reminders are deduplicated.
5. Test email-only and both-off bill preferences with fresh events. Email-only should reach the real inbox/spam folder without a visible in-app item. Both-off should send/show neither.
6. Test linked-user financial alerts on an accepted monitoring relationship while the local app keeps running and the recipient browser is closed. Allow a startup/five-minute scan. Change the recipient's category preferences and use fresh eligible alert events; verify independent channels and that disconnection prevents later access/delivery.
7. Turn invitation email off on account B. From A, send B a monitoring request/shared invitation. B should still see and accept/reject it, while no email is sent.
8. Confirm verification/password-recovery emails still use the existing authentication flow.
9. Open Settings in two tabs, save one, then submit the older tab. Expect a reload message instead of silently overwriting newer preferences.

Validation passed: 30 integration checks and 70 browser checks (100 total), plus the mobile layout checks at 390px and 320px. The build completed with zero warnings/errors. Coverage includes persistence, defaults, account isolation, stale forms, legacy-history migration, independent channels, pending-message cancellation, duplicate prevention, invitation consent with email off, CSRF and browser theme/rendering behavior. Screenshots of Settings, analytics, profile and Login were visually inspected. Real SMTP delivery remains a manual inbox check.
