# Family & Shared Finance — setup and review

## Portfolio coverage

Source: `EdisonWong_Portfolio1Report.pdf`, report pages 74–75 (detailed use cases), 87–88 (create/invitation flow), 107 (class diagram), and 120 (interface). The portfolio file has not been changed.

| Portfolio requirement | Implementation |
|---|---|
| Add shared finance | Name, type, optional description, initial budget or savings target, and invitations |
| Select finance type | Shared Monthly Budget or Shared Savings Goal |
| Invite registered members | Creation accepts 1–10 other active registered users; owner can invite more later |
| Assign member roles | Creator starts as Owner; accepted eligible Members can be promoted to co-owner with confirmation. Existing Owners retain access. |
| Pending consent | Invite remains Pending until that signed-in recipient accepts or rejects |
| Edit/delete shared finance | Any current accepted Owner; deletion requires confirmation |
| Monthly budget | Owner sets a budget for each selected month, preserving other months |
| Shared savings goal | Target, derived savings progress, and Achieved/Active goal status |
| Shared records and contributions | Accepted members record their own spending or savings entries and review group history |
| Ownership and privacy | Pending users cannot view group financial data; ordinary members cannot manage the arrangement |

### Exact class attributes retained

No original class or attribute was removed or renamed. Navigation properties supplement these names.

| Class | Original attributes |
|---|---|
| SharedFinance | `sharedFinanceId`, `sharedFinanceName`, `financeType`, `description`, `createdDate`, `status` |
| SharedFinanceMember | `sharedFinanceId`, `userId`, `memberRole`, `invitationStatus`, `invitedDate`, `joinedDate` |
| SharedBudget | `sharedBudgetId`, `sharedFinanceId`, `budgetAmount`, `budgetMonth` |
| SharedSavingsGoal | `sharedSavingsGoalId`, `sharedFinanceId`, `targetAmount`, `status` |
| SharedFinanceTransaction | `sharedTransactionId`, `sharedFinanceId`, `userId`, `expenseCategoryId`, `transactionType`, `amount`, `transactionDate`, `description` |

SharedFinanceMember retains the composite primary key `(sharedFinanceId, userId)`. Additions are `SharedFinance.ownerId/revision`, invitation version/email-delivery metadata, and transaction expense link/payment method/creation time/submission key. These support ownership, stale-form protection, durable mail claims, personal accounting, and duplicate prevention.

The database keeps one savings goal per savings arrangement and one budget per arrangement/month. Finance type is fixed after creation so existing ledger entries retain their meaning. There is no owner transfer or separate account-level shared-finance role. Accepted membership rows with memberRole Owner grant shared management access. The existing ownerId remains the original creator reference; it is not the sole authorization source and is not changed when promoting or leaving.

## Personal-finance accounting

Personal integration is an accepted enhancement from the development conversation. Shared entries are records of activity the user has already made; the app does not move money or connect to a bank.

### Monthly budget spending

An accepted member records an Expense against a month for which the owner has saved a shared budget. This creates:

1. One SharedFinanceTransaction for the group, attributed to the signed-in member.
2. One linked personal Expense for that same member, category, amount, actual date and method.

The personal Expense naturally appears in Transactions, budgets, analytics, Financial Health, prediction and parental monitoring. It is counted once; the group ledger is not added again as another personal expense. The shared monthly budget is a monitoring limit, not a bank-payment block: recorded spending can exceed it and the overview shows the result.

### Shared savings contributions and withdrawals

- Contribution reserves the signed-in contributor's available funds. It reduces available-to-spend and increases that person's own allocation and the shared goal's saved total.
- Withdrawal releases only the signed-in user's own current net allocation. It increases available funds by reducing the reservation. It is not new income, and it does not create a personal Expense.
- Available funds are recorded income minus recorded expenses, personal savings contributions and net shared savings allocations. A contribution is rejected if it exceeds those available funds or the shared target's remaining amount.
- The full allocation history is included even after group archival or membership changes. No money becomes available merely because a group is hidden or deleted.
- Savings entries must be in date order for that person in that group. Future dates, non-positive amounts and fractional cents are rejected.
- A monthly net savings amount can be negative when a user releases money contributed in an earlier month. This is a cashflow release, not negative income.

Net shared savings allocations are integrated into the personal dashboard, selected-month Transactions summary, analytics, Financial Health, monitoring summaries, and automatic personal-saving funds checks. Personal goal cards and goal-specific contribution histories remain separate; shared funds are not inserted into a fabricated personal SavingsGoal. Other members' contributions never reduce your own available funds.

The Shared Savings category is seeded if absent, preserving an existing category's identity and activation state. New contributions require it active. A historical allocation can still be withdrawn when that category has been deactivated, so reserved funds do not become trapped by category settings.

## Roles, consent and lifecycle

- Owner: create, edit, activate/inactivate, set monthly budgets/target, invite, cancel pending invitations, retry mail, remove eligible Members, promote accepted eligible Members to Owner, record own entries and delete with confirmation. Multiple accepted Owners have the same management access.
- Member: accept/reject their own invitation, review accepted group records, record their own permitted entries, withdraw their own savings and leave.
- Each person must release their own savings allocation before leaving or being removed. An Owner may leave only when another accepted, active and eligible Owner remains. The last active Owner must promote another accepted Member first, or delete the arrangement. Owners cannot remove other Owner memberships; this feature does not add demotion.
- Pending, rejected, cancelled, removed and left states grant no group financial access. Re-invitation uses a new version and requires fresh acceptance. Old invitation actions cannot accept a later request.
- Accepted Owners see pending/rejected membership and invitation-delivery details. Members see accepted participants and the shared ledger; users never receive access to unrelated personal transactions. Owned/Joined filters and role badges use current accepted membership roles.
- A group remains available while at least one accepted Owner has an active eligible account. Deactivating/deleting its original creator does not disable another eligible co-owner. If no eligible Owner remains, group financial access stops; each eligible user can still release their own reserved savings through their retained personal history.
- Inactive arrangements accept no new spending, contributions or Owner promotions. An Owner can reactivate the arrangement. Users can still release their own reserved savings.
- Delete archives the arrangement from normal group views; it does not delete the ledger, linked personal expenses, or reservations. Use **My shared entries** to review your own history and release remaining own savings after archival or when no eligible Owner is available. Full group records remain restricted.
- **View shared entry** in Transactions opens only the signed-in user's retained entry, even after leaving or group deletion.

## Immutability and duplicate protection

Recorded shared entries and linked personal expenses are read-only. Expense Edit/Delete routes reject separate changes to avoid diverging group and personal ledgers. Corrective editing/deletion of posted entries is not exposed in this version; a savings withdrawal releases an allocation but does not erase the original contribution. Review amounts before submitting spending.

Monetary writes use a transaction, ordered account/group locks, and a unique per-user submission key. A repeated or simultaneous submission of the same form cannot create a second entry or linked expense. Group edits/deletion use revision checks. The target, balance and available-funds checks are repeated while locked.

## Invitation email

The existing `IAlertEmailSender`/SMTP configuration is reused. No new email provider or environment setting is required.

Invitation acceptance always requires signing in and choosing Accept for the pending invitation; an email alone grants no access. Group name, invitation status/version, sender/recipient eligibility and active arrangement state are checked under locks before sending. A durable Sending claim prevents competing requests from delivering the same invitation twice. Failed SMTP leaves the pending invitation visible and allows manual retry after one minute. A cancelled/archived/inactive or otherwise unavailable delivery is suppressed. Sent invitations are not resent automatically, and an ambiguous interrupted Sending state is not automatically retried to avoid duplicate mail.

If concurrent promotions repeatedly change the owner lock set before SMTP begins, delivery defers without undoing the saved group/invitation. A pre-send Sending claim is returned to Pending only for that same still-current invitation version; the existing cooldown remains. A later Owner can retry, while Sent/ambiguous Sending claims and newer invitation versions are never reset by that recovery.

Unlike bill reminders, these invitations are sent in response to create/invite/retry actions; there is no periodic shared-finance email worker. Free-tier Azure sleep can delay the request starting, but Always On is not needed for this user-triggered flow. Automated tests use a fake sender; test your real inbox/spam folder separately.

## Local setup

The co-owner update uses the existing `SharedFinanceMember.memberRole` and group revision fields. It adds no table/column, changes no original class attributes, and needs no new database migration. If shared finance already works on your installation, rebuild/restart the app and open Shared Finance.

The original shared-finance schema was introduced by `20261108125926_AddSharedFinance`; apply pending migrations only if the installation is missing existing schema. The co-owner feature does not introduce another migration or require resetting an ID counter.

No actual local application database or Azure database was modified during this update. Validation used isolated disposable databases and fake email. No new feature flag, account-level role, SMTP setting or Azure configuration is required.

### Co-owner promotion

Open an active group as an Owner. In Members, choose **Promote to Owner** beside an accepted, active Member and review the explicit confirmation. Pending invitations cannot be promoted. The current Owner remains an Owner, and no personal/shared monetary record changes.

The confirmation carries the current arrangement revision and invitation version. The service rechecks active accounts, accepted Owner access and the target's current Member role under ordered account/group locks. Stale/replayed confirmations and self/non-owner/outsider requests cannot grant access. Owner account IDs are locked in order before the group; if a concurrent promotion changes that lock set, the operation retries with a fresh set. The group's accepted eligible Owners govern availability instead of the original creator reference.

## Two-account test checklist

Use two separate browser profiles, or a normal window and a private window, for two active registered users A and B.

1. As A, create **Shared Monthly Budget** for the current month with budget RM500 and invite B. Check that A is Owner, B is Pending, and an invitation appears for B. Check the real email inbox/spam folder.
2. Before accepting, B must not be able to open financial details or record an entry. Reject once and verify no access; re-invite after the cooldown, then accept as B. An old version of the invitation must not accept the new one.
3. As B, record spending RM50 in an active expense category. The shared month shows RM50 spent; B's Transactions contains one RM50 expense and **View shared entry**. A's personal expenses do not increase.
4. Before any promotion, confirm B cannot edit/delete the arrangement, change its budget, invite/remove another member or access another user's personal entry. A can update the selected month's budget without overwriting other months.
5. As A, create **Shared Savings Goal** with target RM300 and invite B. Accept as B. Ensure B has recorded available funds.
6. Contribute RM100 as B. Shared saved total and B's reservation rise by RM100; B's available funds fall by RM100. No personal income or expense is created. A's available funds stay unchanged.
7. Withdraw RM40 as B. Shared saved total/reservation become RM60 and available funds recover RM40. Try to withdraw more than B's own RM60 or another member's funds; expect rejection.
8. Try invalid amounts, a future date, a date earlier than B's latest savings entry, a contribution above available funds or above the remaining target, and a duplicate/two-tab submission. Expect errors and no duplicate financial writes.
9. Reach the shared target and confirm 100% progress (the stored goal status becomes Achieved). Withdrawing reopens progress. A cannot reduce the target below the amount currently saved.
10. Try leaving/removing B with a reservation remaining; expect a release-first message. Release B's own remainder, then leave/remove. Full group access ends, but B's personal retained history and linked expenses remain available.
11. Inactivate or delete a savings arrangement while some own funds remain reserved. Confirm no new contribution/spending, reservations remain in personal totals, and each user can release their own funds through **My shared entries**. Check a budget group's archive preserves linked expenses too.
12. Test search/type/owned-or-joined filters, month/type entry filters, Reset, pagination and mobile layouts. Check dashboard/Transactions/analytics savings totals and automatic savings funds checks agree with the net reservation.
13. Simulate an invitation email failure if convenient: pending access stays Pending, failure is shown, and retry is available after the cooldown. Cancellation or inactivity must prevent a later email send.

## Co-owner acceptance checklist

1. Use A as the original Owner and B as an accepted Member. Confirm Cancel on the promotion page leaves B a Member, then confirm promotion. Both accounts must show Owner and management controls.
2. As B, edit the group, save a month budget or target, invite/cancel a third user's invitation, and open Delete confirmation. Confirm Ordinary Members and Pending invitees cannot access these actions or promotion directly.
3. Confirm both Owners appear in Owned groups and neither Owner row offers Remove. A co-owner may promote another accepted eligible Member; invitation acceptance still happens before promotion.
4. Open promotion in two tabs. Change the group's revision in one tab and submit the old confirmation in the other. Expect a reload message and no promotion from that stale form.
5. With no own reservation, let A leave while B remains an eligible Owner. B must retain access even though ownerId still references the original creator. Try leaving as the last active Owner; expect rejection. An inactive/deleted/non-eligible Owner does not satisfy the remaining-owner guard.
6. In a shared savings group, attempt to leave as a co-owner with an own allocation. Expect release-first guidance. Other Owners cannot withdraw that person's allocation. Existing spending, savings and personal history must be unchanged by promotion/leave.
7. Review promotion, member actions, owned/joined filters and role badges on desktop/mobile in light/dark modes.

## Validation and deployment

**Current co-owner verification: 58 existing shared-finance integration checks and 30 new co-owner assertions passed (88 total).** The EF model still matches its existing migrations and the full application builds with zero warnings/errors. Coverage includes promotion permissions/stale forms, concurrent promotion, all co-owner management actions, creator inactivity/deletion, last-active-owner protection, preserved own-only funds and existing accounting/consent behavior. The final browser pass completed 100 assertions plus 8 focused checks with zero JavaScript errors, including promotion confirmation, co-owner edits/invitations/budget changes, membership-role filters, stale confirmation, creator leaving, last-owner controls, and light/dark desktop/320px/390px layouts. Desktop/mobile screenshots were visually reviewed. Another 17 focused invitation-delivery checks passed using injected ownership-change faults, bringing current backend/regression coverage to 105 checks.

The original shared-finance deployment notes below describe its older migration baseline; they are historical guidance, not a new co-owner upgrade requirement. Confirm the actual applied migrations before any later deployment. No Azure action was performed for co-owners.

The offline Azure upgrade script is [Azure-Shared-Finance.sql](D:/Downloads/azure-shared-finance-deploy/Azure-Shared-Finance.sql). It starts from 20261106192119_AddRoutineBillReminders and idempotently includes the pending Debt Management, Calculated Loans, and Shared Finance migrations through 20261108125926_AddSharedFinance. Its reviewed scope adds eight tables, loan metadata, category seeds, and one principalAmount backfill.

Before later Azure deployment, back up Azure MySQL, confirm that baseline, and apply this script before deploying code. Personal summaries now query SharedFinanceTransactions, so deploying against a database without this schema will fail. The application databases remain untouched until you apply the upgrade. No commit or push was performed for this implementation. Keep the Google verification file, and use localhost for review while the site's Safe Browsing classification remains unresolved.
