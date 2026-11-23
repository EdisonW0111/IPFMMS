# Supervisor feedback regression checks

These checks use only `127.0.0.1:33317`, database `feedback_checks`, with a disposable MySQL root account. They create randomized fixtures, apply migrations to that test database and send no real email. Never use a normal local or Azure database. Start an isolated MySQL instance before running.

```powershell
$env:IPFMMS_FEEDBACK_TESTS = "1"
dotnet run --project .\tests\SupervisorFeedbackChecks\SupervisorFeedbackChecks.csproj
```

Covers funds limits, future income exclusion, concurrent savings allocations, debt expense accounting, automatic savings catch-up/retries, repeat budget edit scope and atomic rollback, member percentages and rounding, ownership permissions, notification preferences/deduplication/isolation, requests and bill reminders, and migration/model consistency.
