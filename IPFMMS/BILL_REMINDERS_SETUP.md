# Bill reminders setup

1. Apply `Update-Database` in Visual Studio Package Manager Console. Latest migration: `20261106192119_AddRoutineBillReminders`. It adds only nullable `routineBillId` and `billDueDate` to Notifications, keeping original diagram attributes unchanged.
2. Enable locally after the migration (PowerShell):

```powershell
dotnet user-secrets set "RoutineBills:RemindersEnabled" "true" --project "C:\Users\User\source\repos\IPFMMS\IPFMMS\IPFMMS.csproj"
```

3. Restart the app. For Azure, apply this migration to Azure MySQL before deploying the new code. Then add the App Service app setting `RoutineBills__RemindersEnabled` = `true`. Existing SmtpSettings are reused. No Always On upgrade is required; a sleeping app delays checks and emails until it wakes. Reminders are disabled by default until explicitly enabled.

## Behavior
- Checks immediately on app startup and every five minutes while running, plus notification page/unread refresh. No guaranteed wall-clock delivery on a free sleeping App Service.
- Upcoming alert within reminderPeriod days, due-today alert, then overdue alert: at most one of each per bill ID and scheduled due date. Zero reminder days starts on the due date.
- On wake, only the current stage is created; missed earlier stages are not replayed. Overdue alerts occur once per occurrence, not daily.
- Current eligible stage is shown in Notifications and unread counts. When paid, moved to another date, inactive or deleted, the old occurrence is hidden and pending email will not send. Already delivered email cannot be recalled. Reducing reminder days hides upcoming alerts outside the new window; reactivation of the same occurrence does not create duplicates.
- Payment records and expenses are not created by reminders. No bank account is charged. The existing confirm-payment flow remains required.
- Email uses existing durable Sending/Sent/Failed state and manual retry cooldown. A failure retains the in-app alert. Delivery batches are bounded to three per refresh; a later check continues the queue. Ambiguous interrupted SMTP Sending states are not automatically retried, to avoid duplicate emails.
- No admin behavior or monitoring consent rules changed. Financial alerts continue to require accepted current-version monitoring links.

## Quick local test
Create three bills: due in two days with reminder 3; due today with reminder 0; due yesterday. Restart or open Notifications. Expect upcoming, due-today and overdue alerts with emails. Refresh twice: no duplicates. Mark one due bill paid: its old reminder disappears. Inactivate/delete another: its reminder disappears. Use a test inbox to verify real SMTP delivery; automated verification uses a fake sender and an isolated database.
