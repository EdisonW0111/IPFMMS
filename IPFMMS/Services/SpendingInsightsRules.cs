using IPFMMS.ViewModels;
namespace IPFMMS.Services;

// Deterministic rules. Thresholds and the supporting figures are shown in the page.
public static class SpendingInsightsRules
{
    public static List<SpendingRecommendation> Build(SpendingInsightsViewModel m)
    {
        var results=new List<SpendingRecommendation>();
        void Add(string rule,string severity,string title,string evidence,string suggestion,string action="Budget") =>
            results.Add(new(){Rule=rule,Severity=severity,Title=title,Evidence=evidence,Suggestion=suggestion,Action=action});
        if(m.TransactionCount==0)
        {
            Add("no-expenses","info","Record expenses to start your insights","No expenses were recorded in the selected period.","Choose a month with records or add your expenses. No spending pattern can be inferred yet.","Expense");
            return results;
        }
        foreach(var budget in m.Budgets.Where(b=>b.AmountSpent>b.BudgetLimit).OrderByDescending(b=>b.AmountSpent-b.BudgetLimit).ThenBy(b=>b.CategoryName).Take(3))
            Add("budget-exceeded","danger",$"Review {budget.CategoryName} overspending",
                $"RM {budget.AmountSpent:N2} spent against a RM {budget.BudgetLimit:N2} saved limit; RM {budget.AmountSpent-budget.BudgetLimit:N2} over.",
                "Check the recorded expenses and review whether future spending or the budget needs adjusting. This does not change any records.");
        var near=m.Budgets.Where(b=>b.BudgetLimit>0&&b.Utilization>=80m&&b.AmountSpent<=b.BudgetLimit).OrderByDescending(b=>b.Utilization).FirstOrDefault();
        if(near!=null)Add("budget-near-limit","warning",$"Review the remaining {near.CategoryName} budget",
            $"{near.Utilization:0.##}% used, with RM {near.Remaining:N2} remaining. This rule starts at 80% usage.",
            "Review the remaining allowance before further spending in this category.");
        if(m.IncomeCount==0)Add("income-missing","info","Complete your income records",
            "No income was recorded in this period, so an income-based spending comparison is incomplete.",
            "Add any missing income before drawing conclusions about how spending compares with income.","Income");
        else if(m.TotalExpenses>m.Income)Add("expenses-over-income","warning","Review spending above recorded income",
            $"Expenses of RM {m.TotalExpenses:N2} exceed recorded income of RM {m.Income:N2} by RM {m.TotalExpenses-m.Income:N2}.",
            "Check for missing income or one-off expenses, then review adjustable spending. This comparison excludes opening balances.","Transaction");
        if(!m.HasPatternData)Add("limited-data","info","Keep building your spending history",
            $"{m.TransactionCount} expense(s) across {m.ActiveDays} day(s). Behaviour rules require at least 5 expenses across 3 days.",
            "Keep recording transactions. Totals and budget checks are available, but there is not enough evidence for behaviour-based suggestions.","Expense");
        var unbudgeted=m.Categories.Where(c=>!c.HasBudget&&c.Amount>0).OrderByDescending(c=>c.Amount).FirstOrDefault();
        if(unbudgeted!=null)Add("unbudgeted-category","info",$"Consider a {unbudgeted.Name} budget",
            $"RM {unbudgeted.Amount:N2} was recorded without a matching saved category budget for this month.",
            "If a spending limit would help, create a category budget that fits your priorities. Existing expenses are unchanged.");
        if(m.HasPatternData)
        {
            var concentrated=m.Categories.Where(c=>c.Share>=40m).OrderByDescending(c=>c.Share).FirstOrDefault();
            if(concentrated!=null)Add("category-concentration","info",$"Review spending concentration in {concentrated.Name}",
                $"This category accounts for {concentrated.Share:0.##}% of expenses. This review rule starts at 40%; it does not mean the spending is unnecessary.",
                "Check whether the allocation reflects your needs and planned commitments before changing it.","Analytics");
            if(m.PreviousCount>=5&&m.ComparableCurrentCount>=5&&m.ChangePercent>=20m)
                Add("spending-increase","warning","Check the rise in comparable-period spending",
                    $"Spending increased by RM {m.Change:N2} ({m.ChangePercent:0.##}%) over the displayed comparison periods. This rule starts at 20%.",
                    "Review category changes and one-off transactions before deciding whether the increase needs action.","Transaction");
            var frequent=m.Categories.Where(c=>c.Count>=5).OrderByDescending(c=>c.Count).ThenByDescending(c=>c.Amount).FirstOrDefault();
            if(frequent!=null)Add("frequent-category","info",$"Review frequent {frequent.Name} purchases",
                $"{frequent.Count} transactions, averaging RM {frequent.Average:N2}. This review rule starts at 5 transactions in one category.",
                "Review the entries for repeated small purchases or duplicates. Frequency alone does not indicate a problem.","Transaction");
        }
        if(results.Count==0)Add("review-plan","info","Keep reviewing your plan",
            "None of the current recommendation thresholds were triggered by the recorded data.",
            "Continue tracking expenses and review your budget targets as your circumstances change.","Analytics");
        return results;
    }
}
