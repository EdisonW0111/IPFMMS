# Debt Management — implementation and review

## Portfolio coverage
Source: EdisonWong_Portfolio1Report.pdf, report pages 75–76 (use cases), 86–87 (repayment validation), 95 (activity), 102–103 (sequence), 107 (class diagram), 121 (UI).

| Required behavior | Implementation |
|---|---|
| Add, edit, delete personal debts | DebtController; deletion requires confirmation and archives history |
| Record repayment, prefill monthly amount | Repay GET defaults to min(monthly plan, outstanding) |
| Reject zero/negative/over-balance repayments | Server validation under serialized owner lock |
| Corresponding expense under Debt Repayment | One linked expense with actual paid date, method, amount |
| Update balance, history, status | One transaction; zero balance becomes Paid |
| Summary: outstanding, total paid, monthly plan, due date, progress | Index cards, list, debt detail and retained history |
| Access only own debt | Claim-based ownership on every route and service; User/Parent roles |
| Original class names and attributes | All 12 Debt and 7 DebtRepayment diagram attributes preserved verbatim |

## Added fields
Debt.openingOutstandingAmount separates amounts repaid before tracking from new recorded repayments. Debt.revision detects stale edit/payment/delete forms. DebtRepayment.createdDate orders same-day history. No diagram attributes were removed or renamed. DebtRepayment.expenseId remains nullable to preserve the diagram's optional association, but all repayments made through this module create a linked expense.

## Manual tracking choices (existing records)
- This tracks debts you owe; it does not issue loans or automatically create income when a debt is added.
- Original/outstanding/monthly amounts support cents exactly. Starting outstanding may be below original; their difference is earlier repayment, not a new expense.
- In Manual mode only, annual interest rate is stored for reference, not automatically accrued. The portfolio does not define an accrual or amortization formula. Outstanding reflects the user-entered opening balance minus recorded repayments.
- No automatic deductions. Each manually confirmed repayment creates one Debt Repayment expense. Future payment dates and overpayment are rejected; early repayment of an existing debt is allowed.
- Optional Next payment due date is an additional scheduling field on the repayment form, not a new database attribute. Set a later date when a payment advances your plan, or leave blank to keep the due date. Full repayment ignores this field.
- Once any repayment exists, outstanding is read-only even on edit. Correct name, description, plan, interest, due date, and activation status without rewriting history. Original cannot be below the opening tracked balance. Repayment editing/deletion is not required by the portfolio and is not exposed.
- Inactive debt remains in total outstanding; planned monthly total uses Active only. Deleted debt is excluded from summaries; its history and expense remain accessible from Transactions → View repayment.
- The Debt Repayment category is added if missing, respecting an existing category's ID and inactive status. The category seeder defaults also include it. Inactive/missing category blocks repayment with a clear error.
- Search, status/type filters and repayment-month filtering update via AJAX, preserve pagination/Back/Forward, provide error feedback and work without JavaScript via Filter buttons.
- Existing monitoring, bill reminders, admin behavior, and portfolio file are unchanged. No extra debt reminder system or interest charges are invented.

## Local setup
Stop the app and run in Visual Studio Package Manager Console:

```powershell
Update-Database
```

Latest base-debt migration: `20261107114918_AddDebtManagement`. Calculated loans additionally require `20261107133236_AddLoanCalculations`. See LOAN_CALCULATOR_SETUP.md for the enhancement. It creates Debts and DebtRepayments, ownership/history indexes and restrictive foreign keys, and seeds Debt Repayment if absent. It does not delete existing data or rewrite existing categories. No additional environment setting is required. Restart and choose Debt Management in the sidebar.

## Review checklist
1. Add original 1,000 / outstanding 800 / monthly 200 / interest 3.5 / due today. Expect earlier paid 200 and no new income/expense.
2. Record repayment 200. Expect outstanding 600, history 200 and one Debt Repayment expense on the chosen date. Transactions offers View repayment, not Edit.
3. Try 0, negative, more than balance, fractional cents and a future payment date. Expect errors and no writes.
4. Submit the same form from two tabs. Expect one repayment only; refresh details before another intentional payment.
5. Repay remaining balance. Expect outstanding 0, Paid status, 100% progress and no repayment action.
6. Edit metadata; verify recorded outstanding is read-only and history is retained. Inactivate a debt; no repayment action is available until reactivated.
7. Check search/status/type filters, Reset, pagination and history payment-month filter on desktop/mobile.
8. Delete with confirmation. List summary excludes it while Transactions still links to read-only archived history. Other users cannot access it.
9. Review the optional next-due-date behavior and interest assumption above; these can be enhanced after testing.

## Deployment
Apply Azure schema migration after a backup and before deploying this code, because Transactions now queries DebtRepayments. Do not deploy the new code against an unmigrated Azure database. Keep the Google verification file and do not bypass the unresolved Safe Browsing warning to test this module; use localhost during Google's review.
