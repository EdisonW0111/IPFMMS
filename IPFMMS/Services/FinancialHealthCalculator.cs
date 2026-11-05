using IPFMMS.ViewModels;
namespace IPFMMS.Services;
public static class FinancialHealthCalculator
{
    public const string Version="FHS-1";
    public static decimal Score(decimal value)=>decimal.Round(Math.Clamp(value,0m,100m),2,MidpointRounding.AwayFromZero);
    public static string Rating(decimal score)=>score>=80m?"Excellent":score>=60m?"Good":score>=40m?"Fair":"Needs attention";
    public static void Calculate(FinancialHealthViewModel m)
    {
        m.MissingData.Clear();m.SavingsScore=null;m.BudgetScore=null;m.StabilityScore=null;m.Overall=null;m.SpendingVariation=null;m.Rating="Not enough data";
        if(m.Income>0m&&m.IncomeCount>0)m.SavingsScore=Score(m.Savings/m.Income/.20m*100m);
        else m.MissingData.Add("Record a positive income total for this month to calculate the savings ratio.");
        if(m.BudgetCount>0)m.BudgetScore=Score((decimal)m.BudgetsWithinLimit/(m.BudgetCount+m.UnbudgetedCategories)*100m);
        else m.MissingData.Add("Save at least one category budget for this month to calculate budget adherence.");
        if(m.StabilityMonths.Count==3&&m.StabilityMonths.All(x=>x.Count>0))
        {
            double mean=m.StabilityMonths.Average(x=>(double)x.Amount);
            if(mean>0){
                double variance=m.StabilityMonths.Average(x=>Math.Pow((double)x.Amount-mean,2));
                decimal variation=(decimal)(Math.Sqrt(variance)/mean);
                m.SpendingVariation=variation*100m;m.StabilityScore=Score((1m-variation)*100m);
            }
        }
        if(!m.StabilityScore.HasValue)m.MissingData.Add("Record expenses in each of the three completed months shown below to calculate spending stability. A month without entries is treated as missing data.");
        if(m.SavingsScore.HasValue&&m.BudgetScore.HasValue&&m.StabilityScore.HasValue){
            m.Overall=Score(m.SavingsScore.Value*.40m+m.BudgetScore.Value*.30m+m.StabilityScore.Value*.30m);
            m.Rating=Rating(m.Overall.Value);
        }
    }
}
