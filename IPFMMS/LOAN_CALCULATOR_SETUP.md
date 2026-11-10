# Malaysian loan calculation enhancement

## Setup
Stop the app, run `Update-Database` in Visual Studio Package Manager Console, and restart. Latest migration: `20261107133236_AddLoanCalculations`. It preserves all original Debt and DebtRepayment attributes. It adds calculation/rate/term metadata, allocation snapshots, and DebtRateChanges. Existing debts default to Manual, and existing repayments' new principalAmount is backfilled from their original repaymentAmount; existing amounts, dates, expenses and balances are unchanged. No new environment setting or external rate API is required.

## Product behavior
- **Calculated loan:** monthly reducing balance with Fixed or Variable (floating) rate. Original principal, term and annual rate derive the monthly estimate on the client and independently on the server. A forged browser estimate is not trusted.
- **New loan:** outstanding principal is automatically the original financed principal. Future interest is not added to principal at creation.
- **Existing loan:** enter the lender's remaining principal and remaining term once, with a tracking start date. Do not enter remaining contractual instalments including future interest as principal.
- **Manual tracking:** keeps earlier records unchanged and allows tracking a simple debt or an older flat-rate agreement using lender figures. Its stored interest is informational; cash reduces the entered balance. Existing manual repayments cannot be silently converted into calculated history.
- **Floating rate:** annual rate = entered reference rate + entered spread, with named SBR/BR/BLR/Other reference. These are user-entered contract values, not live OPR data. Update floating rate on details to re-estimate the remaining term starting at the next unopened, non-overdue instalment. The effective scheduled date, old/new rate and new instalment are audited. An opened/partially paid or overdue period must be completed at its existing rate first. Historic repayments/expenses are immutable.
- **Legacy flat-rate comparison:** calculator only, fixed rate only. It estimates total contractual interest and instalment; it does not calculate Rule-of-78 early-settlement rebates or goodwill discounts, and does not automatically track an older agreement as a new reducing-balance loan. Use Manual mode with bank figures for that agreement.

## Formula and repayment assumptions
For principal P, annual lending rate a (percent), n remaining monthly periods: r = a / 1200; monthly payment = P*r/(1-(1+r)^(-n)), or P/n at zero rate. Money is rounded to cents AwayFromZero. The schedule uses rounded monthly interest and adjusts the last instalment to reconcile to zero principal. Flat comparison: interest = P*(a/100)*(n/12); monthly estimate = (P+interest)/n. A flat percentage and a reducing-balance lending percentage with the same numeric value are not equivalent rates.

Each calculated repayment covers the scheduled period's unpaid monthly interest first, then principal. Interest is charged once per scheduled period, not once per submission. The full cash amount creates one Debt Repayment expense; principal/interest splits are snapshots, not extra expenses. Partial payments stay in the same period; cumulative cash reaching the scheduled instalment advances one month. Extra cash reduces principal and can shorten the projected term; it does not skip or pre-charge multiple future instalments. Month-end anchors are retained (Jan31 -> Feb28 -> Mar31). A final payment includes current unpaid interest; zero principal becomes Paid. Actual payment dates cannot be future, before tracking starts, or earlier than an existing calculated repayment (same-day payments allowed). No opening/viewing a page creates financial writes.

The projection assumes the entered rate stays unchanged. This is an equal-month planning model, not daily-rest bank servicing, a lender statement, an approved product/eligibility check or an early-settlement quote. No fees, insurance, compounding arrears/default interest, penalties, contractual rebates, Islamic profit-contract rules or flexi offset/redraw facilities are implemented. The bank's agreement and statement take priority. Bank-specific annual/EIR labels and effective-yield calculations can differ from the model's explicit annual-rate/12 convention.

## Malaysia source basis (checked 7 November 2026)
- BNM 2026 hire-purchase consumer guide: https://www.bnm.gov.my/documents/20124/40668/HP%2BConsumer%2BGuide_EN_2026.pdf
- ABM transition statement: https://www.abm.org.my/press-releases/banks-in-malaysia-introduce-changes-to-hire-purchase-financing-with-early-settlement-discounts/
- BNM monetary stability / fixed and floating explanation: https://www.bnm.gov.my/en/monetary-stability
- BNM reference rate framework: https://www.bnm.gov.my/en/-/pd-revised-reference-rate
- UOB home loan PDS (daily rest): https://www.uob.com.my/assets/web-resources/personal/pdf/useful/product-disclosure-sheet-home-loan-hl-eng.pdf

Hire-purchase changes took effect 1 June 2026 with transition through 31 March 2027. Both fixed and floating reducing-balance products exist. The transition is why a labelled legacy-flat comparison remains available. This module does not claim every Malaysian loan uses one universal accrual/settlement formula.

## Review examples
1. Calculator: RM20,000 / 60 months / fixed2.98% reducing -> RM359.20/month; estimated total interest RM1,551.80 with final rounding adjustment. Legacy flat comparison at2.98% -> RM383/month and interest RM2,980. These are different methods, not equivalent-rate offers.
2. Add a calculated new loan with those inputs: balance is automatic20,000 and instalment readonly359.20. Payment359.20 allocates first-period interest49.67 and principal309.53; balance19,690.47.
3. Pay only10 first: principal stays20,000; next payment349.20 covers only remaining interest39.67 and principal309.53. Refreshing does not add another month's interest.
4. Floating: reference2.75 + spread1.50 ->4.25%; after finishing one period, update reference3.00 ->4.50%, recalculate future instalments over remaining term, preserve previous rate allocations.
5. One-month loan1,000 at12%: final monthly estimate1,010, interest10, principal1,000, balance0/Paid.
6. Existing loan: original20,000 / remaining principal10,000 / remaining24months: derive from10,000 and24, not from original age. Principal before tracking is separated from current cash repayments.
7. Check Manual mode retains old functionality; filters, Transactions View repayment, archive history and owner protection continue to work.

## Deployment
Apply both debt migrations and the loan enhancement to Azure after backup, before deploying code. A combined offline-generated script is provided in Downloads/azure-debt-loans-deploy. No Azure/local application database has been modified by development checks; tests use isolated MySQL33317 only. Do not bypass the site's unresolved Safe Browsing warning for live testing.
