# Chapter 4 class attribute alignment

Figure 4.24's 21 original entity classes and all 156 original attribute names are available exactly as shown, including lower camelCase spelling. No original implementation property, database column, stored record or existing consumer was removed or renamed.

Nine older classes previously used PascalCase property names. They now also expose 68 diagram-facing forwarding properties. Each is marked `[NotMapped, JsonIgnore]` and reads/writes the existing property directly. There is one value, not a second backing field or a new SQL column. The other twelve original classes already used the diagram's names. Additional supporting classes/fields remain permitted extensions.

| Original class | Exact diagram attributes now exposed by forwarding properties |
|---|---|
| User | `userId`, `fullName`, `email`, `passwordHash`, `phoneNumber`, `role`, `createdDate`, `accountStatus` |
| EmailOTP | `otpId`, `userId`, `otpCode`, `purpose`, `createdDateTime`, `expiryDateTime`, `isVerified` |
| Income | `incomeId`, `userId`, `incomeCategoryId`, `amount`, `incomeDate`, `description`, `paymentMethod`, `reference`, `createdDate` |
| Expense | `expenseId`, `userId`, `expenseCategoryId`, `amount`, `expenseDate`, `paymentMethod`, `description`, `receiptPath`, `createdDate` |
| IncomeCategory | `incomeCategoryId`, `categoryName`, `description`, `status`, `displayOrder` |
| ExpenseCategory | `expenseCategoryId`, `categoryName`, `description`, `status`, `displayOrder` |
| Budget | `budgetId`, `userId`, `expenseCategoryId`, `budgetMonth`, `budgetType`, `budgetAmount`, `budgetPercentage`, `createdDate`, `notes` |
| SavingsGoal | `savingsGoalId`, `userId`, `goalName`, `goalType`, `targetAmount`, `targetDate`, `savingsMethod`, `monthlyTargetAmount`, `status`, `createdDate` |
| SavingsContribution | `contributionId`, `savingsGoalId`, `amount`, `contributionDate`, `contributionMethod`, `description` |

## Category status

`IncomeCategory.status` and `ExpenseCategory.status` expose the diagram's original string attribute over the existing mapped boolean `IsActive`:

| Diagram property | Existing persisted value |
|---|---|
| `status = "Active"` | `IsActive = true` |
| `status = "Inactive"` | `IsActive = false` |

Changes work in both directions immediately. Other strings, different casing, empty strings and null are rejected by the `status` setter before the existing state changes. This restores the model API name without introducing an independently stored status that could disagree with IsActive.

## Persistence and compatibility

No new migration is required for this alignment. Existing PascalCase properties remain EF's mapped properties and retain their data annotations and SQL columns. Continue using those mapped properties in LINQ-to-Entities queries, EF updates and existing forms. The forwarding properties are an exact Chapter 4 API for in-memory entity use, not independently queryable EF members. Existing JSON payloads remain unchanged because aliases are ignored during JSON serialization/deserialization.

The original `status` attribute now exists on each category entity, while its database representation remains the existing IsActive column. This is an additive compatibility design, not a claim that a separate column named status was introduced. Existing local/Azure database data and migration history were not modified.

## Validation

A standalone .NET 8 project linked the current entity source files and passed **713 checks**:

- All 21 original class names and all 156 exact attribute names have public getters/setters.
- All 68 aliases forward values in both directions and have no independent backing fields.
- Nullable values propagate; category state maps both ways and rejects unsupported values without mutation.
- All aliases are excluded from EF by NotMapped and ignored by JSON; original mapped properties remain present.
- Default and ASP.NET-style Web JSON serialization succeed without case-only property collisions and preserve the existing payload shape.

Reproducible developer checks: `D:\Downloads\class-diagram-checks\Checks.csproj`. Exact property manifest: `D:\Downloads\class-diagram-property-manifest.json`. Forwarding manifest: `D:\Downloads\class-diagram-forwarding-manifest.json`.

The main task separately validates the full application build and EF model against the current migration snapshot. No actual application database, Azure resource, Git commit or push was changed by these checks.
