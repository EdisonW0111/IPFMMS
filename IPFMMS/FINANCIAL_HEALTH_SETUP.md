# Financial health scoring (FHS-1)

Apply the generated AddFinancialHealthRecord migration locally with `Update-Database` before opening the updated app. Deploy the matching migration to Azure before deploying this version there. No database migration has been applied by Codex to the local application or Azure database.

Figure 4.24's FinancialHealthRecord class is retained with exact lower-camel-case attributes: healthRecordId, userId, savingsRatioScore, budgetAdherenceScore, spendingStabilityScore, healthScore, rating, recordMonth and calculatedDate. The additional formulaVersion attribute identifies the calculation used for a saved snapshot. Users have many records; every record belongs to one User. A unique userId/recordMonth index prevents duplicate monthly snapshots, and the owner FK uses restrictive deletion.

The portfolio's Chapter 2, report page 27, specifies the weights: savings ratio 40%, budget adherence 30%, spending stability 30%. The component normalizations and rating bands below are implementation choices. They are disclosed in the score page and can be revised deliberately in a future formula version.

- Savings ratio: actual manual/automatic contributions dated within the month divided by recorded income. Divide that ratio by a 20% target and multiply by 100. Cap at 100. A zero or absent income total makes the component unavailable; no contribution is counted from a monthly planning target alone.
- Budget adherence: saved category budgets whose spending is within their limit divided by saved budget categories plus spent categories with no saved budget, multiplied by 100. At least one saved budget is needed. A spent category without a budget contributes to the denominator but earns no adherence points. Percentage budgets use income recorded for that period. Viewing health never generates repeating budgets.
- Spending stability: `(1 - population standard deviation / mean) * 100` for three completed calendar-month expense totals. Each of those months must contain at least one expense record. Missing months are unavailable, not substituted with zero. Current-month previews use the preceding three complete months; a completed-month score uses that month and its two predecessors.

Components are clamped to 0-100 and rounded to two decimal places before applying the 40/30/30 weights. The final score is rounded to two decimals. Ratings: Excellent >=80; Good >=60; Fair >=40; Needs attention <40. All three components are required; missing weights are not redistributed. The indicator is system-defined and reflects recorded data rather than a professional credit rating.

The current month is provisional through the current Malaysia date and is never saved. Opening Financial Health automatically refreshes all completed months with saved budgets, plus existing history months. This is on-demand processing, not a background scheduler; history catches up when the page is opened even after the app has slept. The main dashboard remains a read-only preview.

Refresh uses the authenticated owner, a serializable transaction and the existing user row lock. It recalculates on the server and upserts one record per owner/month. Unchanged scores retain their calculatedDate; changed scores retain their ID. If corrected or deleted source data makes a month incomplete, its saved score is removed rather than left misleading. No score/owner values come from a form. The manual Save endpoint and button have been removed. No schema change or additional migration is required for automatic saving.

Manual verification:
1. Open Financial Health with an eligible completed month: history should appear without clicking Save.
2. Reload: there should still be exactly one record per month, with unchanged calculatedDate.
3. Correct a past contribution or budget and reopen: the existing record should refresh if its score changes.
4. Remove required data and reopen: the incomplete month should disappear from history.
5. Check that the current month remains provisional and never appears in history.
6. Use another account and confirm its history remains separate.

Tests use a separate disposable MySQL instance on localhost:33317 and the health_checks database, never application credentials or records. SpendingPrediction remains a separate upcoming implementation.
