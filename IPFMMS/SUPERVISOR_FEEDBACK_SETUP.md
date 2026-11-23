# Supervisor feedback fixes — November 2026

## Database update before running this version

Latest migration: `20261122135936_AddMonitoringDisconnectPreferences` (after `20261116195046_AddFinancialAlertsAndMemberShares`).
The earlier migration adds `UserPreferences.FinancialInApp` (default true), `UserPreferences.FinancialEmail` (default false), and nullable `SharedFinanceMembers.sharePercentage`. The latest migration adds `UserPreferences.MonitoringDisconnectInApp` and `UserPreferences.MonitoringDisconnectEmail`, both defaulting to true. No existing diagram class or attribute is removed or renamed.

In Visual Studio **Package Manager Console**, select IPFMMS as the default project and use your local migration account connection:

```powershell
Update-Database 20261122135936_AddMonitoringDisconnectPreferences
```

The migration was tested on a disposable MySQL instance. Your normal local and Azure databases have not been changed. Do not push this version to the auto-deploying branch until the Azure migration is applied during your chosen deployment.

## Changes and review checklist

1. **Available funds**: income dated through today minus expenses dated through today, minus personal savings contributions and net shared savings allocations. Future income does not fund today's payments. Savings target amounts may be larger than today's available funds; actual contributions and debt repayments are capped. Debt repayments already create expenses and are counted once. Try an amount one cent above the balance, then an affordable amount; rejected writes leave records unchanged.
2. **Automatic savings**: an overdue date in the current month catches up while the app runs, up to the goal target date. Insufficient funds leave one retryable monthly attempt; adding funds allows the next check to save it once. Existing current-month MissedOffline attempts can recover. Older missed months remain skipped. Check contribution history and the dashboard available balance, not just the goal target. An allocation reserves funds inside IPFMMS and does not transfer money through a bank.
3. **Notifications**: own budget overspending, provisional low financial health, debt upcoming/due/overdue reminders, automatic savings results, shared invitations and monitoring requests are included. Existing bill and linked-user alerts remain. In Settings, My financial alerts has separate in-app and email choices; email starts off for this new category. Invitation email stays with its existing service/preferences. When either participant disconnects an active monitoring relationship, the other participant receives an update according to Settings → Monitoring disconnections, with independent in-app and email switches. Disconnection email uses the existing SMTP service and queued delivery worker (up to the next five-minute check while running). Disconnection history remains available after consent ends and contains no financial summary link. Email opt-out cancels waiting messages; failed in-app emails can be retried after the cooldown. Alert messages record the detected event; follow their links for current status. Repeated checks do not create duplicates. In-app refresh does not wait for SMTP.
4. **Budget editing**: default is This month only; the repeat template and saved future budgets stay unchanged. Choosing This month and future months updates saved budgets linked to the same series plus its future template. Past and separately created budgets stay unchanged. Percentage limits are checked in every affected saved month before any changes are committed. Repeat until controls generation of new months; already saved future budgets remain and receive the edited values.
5. **Required fields**: red stars follow server required metadata and conditional HTML requirements throughout shared layouts and standalone authentication pages. Disabled, hidden, optional and calculated read-only fields have no star. Check both fixed and variable calculated-loan fields.
6. **AJAX controls**: redundant Transaction and Spending Insights submit buttons are hidden once automatic filtering initializes. Existing automatic filter pages keep their current behavior. Reset and genuine submit/retry actions remain. No-JavaScript fallback submission is preserved.
7. **Shared percentages**: open a group as an accepted Owner, choose Set member shares, enter e.g. A 60 / B 40 and save. All accepted members must be included and total exactly 100%. Expected amounts are shown next to actual spending or net savings; cent rounding preserves the target total. Shares never charge members or alter payments. An accepted membership change resets the plan for review. Co-owners can manage it; ordinary members cannot.

## Running and Azure behavior

Workers check at startup and every five minutes while the process runs; the badge refreshes about every 60 seconds while the page is visible. Refreshing Notifications also checks eligible events. An Azure Free app can sleep, so these checks are not guaranteed to run at a specific time. This change handles current-month automatic savings catch-up when it resumes.

An explicit `AutomaticSavings:Enabled=false` or `RoutineBills:RemindersEnabled=false` still pauses that feature. Both now default to enabled when absent; appsettings already enables automatic savings. When deploying, confirm any Azure overrides `AutomaticSavings__Enabled` and `RoutineBills__RemindersEnabled` are true if those features should run. No secrets or Azure settings were changed here.

## Verification

The application builds with zero warnings/errors. Regression checks are in `tests/SupervisorFeedbackChecks`; they require opt-in and use only the disposable `localhost:33317/feedback_checks` database, never real data. Chrome checks cover conditional stars, budget scope, AJAX filters, percentage form submission and desktop/390px/320px layouts. Real SMTP delivery and Azure scheduling still need deployment testing.
