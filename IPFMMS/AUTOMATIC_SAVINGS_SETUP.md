# Automatic monthly savings

Apply the generated `AddAutomaticMonthlySavings` migration before running the updated site. In Visual Studio Package Manager Console, with IPFMMS selected as the default project:

```powershell
Update-Database
```

Automatic processing is deliberately disabled in `appsettings.json` until the schema has been updated. After updating the database, set:

```json
"AutomaticSavings": { "Enabled": true }
```

Restart the application after changing this setting. On Azure App Service, the equivalent application setting is `AutomaticSavings__Enabled=true`. Keep the app running; for idle-independent scheduling on App Service, enable Always On on a supporting plan. This implementation does not provision or change Azure resources. Microsoft describes Always On at https://learn.microsoft.com/azure/app-service/configure-common.

The hosted worker checks due schedules on startup and every five minutes, using Malaysia time. Multiple instances use the same user-row lock, transactions and monthly attempt check to avoid duplicate allocations. The unique goal/due-date index is additional protection. Existing automatic-target goals are not silently enrolled: edit the goal and choose a saving day from 1 to 28. First saving is on the next occurrence, including today. If this month's attempt has already been processed, changing the day does not cause another deduction this month.

The monthly amount is calculated from remaining savings and eligible deduction dates on or before the target date. Routine name/type edits preserve the existing monthly plan. Changing the target amount/date/day or enabling automatic saving creates a new plan from the remaining amount; savings to date are already incorporated in that calculation.

Extra manual savings reduce the next scheduled deduction. For a RM200 plan, RM250 extra savings covers the next month and leaves RM50 credit, reducing the following deduction to RM150. Editing/deleting an extra entry changes pending credit, while past automatic records remain unchanged. Credit already used by earlier deductions is not reversed: future extra savings first replace that previously used credit. A replan incorporates the saved total once, avoiding double-counting extra savings.

Successful automatic contributions have ContributionMethod=Automatic and cannot be edited or deleted through either GET or POST actions, even if the goal later switches to Manual. Extra contributions remain Manual and editable/deletable. Goal deletion also preserves automatic attempt history.

Automatic allocations do not modify Income/Expense records or transfer money from a bank. They reduce available-to-spend funds through contribution totals. The worker checks recorded income/expenses through the processing date and all existing savings allocations. If available funds cannot cover the deduction, that month's attempt is marked InsufficientFunds and is not retried. Extra credit is retained for future months. No negative balance is created by the automatic deduction itself. Later user edits or manual allocations may still change available funds.

Dates missed while the app was offline are recorded as MissedOffline, without retroactive deductions. Restarting on the due date still permits processing that day. Catch-up is bounded to 12 dates per goal and 100 goals per run. No automatic deductions run after the target date; users can extend it by editing the goal. Completed goals stop receiving automatic contributions. The latest 12 attempt results, including skipped deductions, appear in Contribution History; this is an in-app status, not an email or push notification.

Testing after migration and enabling:

1. Create an automatic goal with today's saving day (if today is day 1-28) and a future target date. Wait up to five minutes with the app running.
2. Verify an Automatic contribution appears, and has no edit/delete actions.
3. Verify the available-to-spend amount decreases after refreshing the dashboard.
4. Add a Manual contribution to another automatic goal before its due day. Check the estimated next deduction and extra credit.
5. Edit/delete the Manual entry; pending credit should adjust. Cancelling deletion should leave it intact.
6. Restart the app after a successful run; it should not create a second deduction for the same month.
7. Review insufficient-funds attempts in Contribution History.

Automated checks were run against an isolated MySQL instance on localhost:33317, with a separate Downloads data directory. The existing application database was not modified. The generated migration was applied in that isolated instance, not the user's database.
