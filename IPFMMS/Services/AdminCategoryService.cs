using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace IPFMMS.Services;

public sealed class AdminCategoryService(ApplicationDbContext db, IdGeneratorService ids)
{
    public static string ValidateKind(string? kind) => kind is "Income" or "Expense" ? kind
        : throw new ArgumentException("Choose Income or Expense categories.");
    private static bool Protected(string kind, string name) => kind == "Expense" &&
        (string.Equals(name, "Debt Repayment", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(name, "Shared Savings", StringComparison.OrdinalIgnoreCase));
    private static string Stamp(AdminCategoryRowViewModel row) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { row.CategoryId, row.CategoryName,
            row.Description, row.IsActive, row.DisplayOrder, CreatedTicks = row.CreatedDate.Ticks }))));
    private static void CheckStamp(AdminCategoryRowViewModel row, string stamp)
    {
        if (string.IsNullOrEmpty(stamp) || !string.Equals(Stamp(row), stamp, StringComparison.Ordinal))
            throw new InvalidOperationException("This category changed after you opened it. Reload the page before saving.");
    }
    private static (string Name, string? Description) Validate(AdminCategoryFormViewModel model)
    {
        var name = (model.CategoryName ?? "").Trim();
        var description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        if (name.Length == 0 || name.Length > 100) throw new InvalidOperationException("Enter a category name of 1–100 characters.");
        if (description?.Length > 255) throw new InvalidOperationException("Description must not exceed 255 characters.");
        if (model.DisplayOrder < 0) throw new InvalidOperationException("Display order cannot be negative.");
        return (name, description);
    }
    private IQueryable<AdminCategoryRowViewModel> Rows(string kind) => kind == "Income"
        ? db.IncomeCategories.AsNoTracking().Select(c => new AdminCategoryRowViewModel {
            CategoryId=c.IncomeCategoryId, CategoryName=c.CategoryName, Description=c.Description,
            DisplayOrder=c.DisplayOrder, IsActive=c.IsActive, CreatedDate=c.CreatedDate,
            UsageCount=db.Incomes.Count(i=>i.IncomeCategoryId==c.IncomeCategoryId) })
        : db.ExpenseCategories.AsNoTracking().Select(c => new AdminCategoryRowViewModel {
            CategoryId=c.ExpenseCategoryId, CategoryName=c.CategoryName, Description=c.Description,
            DisplayOrder=c.DisplayOrder, IsActive=c.IsActive, CreatedDate=c.CreatedDate,
            UsageCount=db.Expenses.Count(e=>e.ExpenseCategoryId==c.ExpenseCategoryId)
                +db.Budgets.Count(b=>b.ExpenseCategoryId==c.ExpenseCategoryId)
                +db.BudgetSchedules.Count(s=>s.ExpenseCategoryId==c.ExpenseCategoryId)
                +db.RoutineBills.Count(b=>b.expenseCategoryId==c.ExpenseCategoryId)
                +db.SharedFinanceTransactions.Count(t=>t.expenseCategoryId==c.ExpenseCategoryId) });
    private static void Complete(string kind, AdminCategoryRowViewModel row)
    { row.IsProtectedName=Protected(kind,row.CategoryName); row.EditStamp=Stamp(row); }

    public async Task<AdminCategoryIndexViewModel> ListAsync(string actor,string kind="Income",string? search=null,
        string status="All",int page=1,CancellationToken ct=default)
    {
        await AdminGuard.RequireAsync(db,actor,ct); kind=ValidateKind(kind); search=search?.Trim();
        if(search?.Length>100)throw new InvalidOperationException("Search must not exceed 100 characters.");
        if(status is not ("All" or "Active" or "Inactive"))throw new InvalidOperationException("Choose a valid category status.");
        var query=Rows(kind);
        if(!string.IsNullOrEmpty(search))query=query.Where(c=>c.CategoryName.Contains(search)||(c.Description!=null&&c.Description.Contains(search)));
        if(status!="All")query=query.Where(c=>c.IsActive==(status=="Active"));
        var result=new AdminCategoryIndexViewModel {Kind=kind,Search=search,Status=status,Count=await query.CountAsync(ct)};
        result.Page=Math.Clamp(page,1,result.Pages);
        result.Categories=await query.OrderBy(c=>c.DisplayOrder).ThenBy(c=>c.CategoryName).ThenBy(c=>c.CategoryId)
            .Skip((result.Page-1)*20).Take(20).ToListAsync(ct);
        foreach(var row in result.Categories)Complete(kind,row);
        return result;
    }
    public async Task<AdminCategoryFormViewModel?> GetFormAsync(string actor,string kind,string id,CancellationToken ct=default)
    {
        var row=await GetDeleteAsync(actor,kind,id,ct);
        return row==null?null:new AdminCategoryFormViewModel {Kind=row.Kind,CategoryId=row.CategoryId,
            CategoryName=row.CategoryName,Description=row.Description,DisplayOrder=row.DisplayOrder,
            IsActive=row.IsActive,IsProtectedName=row.IsProtectedName,EditStamp=row.EditStamp};
    }
    public async Task<AdminCategoryDeleteViewModel?> GetDeleteAsync(string actor,string kind,string id,CancellationToken ct=default)
    {
        await AdminGuard.RequireAsync(db,actor,ct); kind=ValidateKind(kind);
        if(string.IsNullOrWhiteSpace(id)||id.Length>20)return null;
        var row=await Rows(kind).SingleOrDefaultAsync(c=>c.CategoryId==id,ct);if(row==null)return null;
        Complete(kind,row);
        return new AdminCategoryDeleteViewModel {Kind=kind,CategoryId=row.CategoryId,CategoryName=row.CategoryName,
            Description=row.Description,DisplayOrder=row.DisplayOrder,IsActive=row.IsActive,CreatedDate=row.CreatedDate,
            IsProtectedName=row.IsProtectedName,UsageCount=row.UsageCount,EditStamp=row.EditStamp};
    }
    private async Task UniqueAsync(string kind,string name,string? except,CancellationToken ct)
    {
        var normalized=name.ToUpperInvariant();
        if(await Rows(kind).AnyAsync(c=>c.CategoryId!=except&&c.CategoryName.Trim().ToUpper()==normalized,ct))
            throw new InvalidOperationException("A category with this name already exists. Choose another name.");
    }
    private static bool DatabaseError(Exception error,int number)
    { for(Exception? current=error;current!=null;current=current.InnerException)if(current is MySqlException sql&&sql.Number==number)return true;return false; }
    private async Task SaveChangesAsync(CancellationToken ct)
    {
        try{await db.SaveChangesAsync(ct);}
        catch(DbUpdateException ex)when(DatabaseError(ex,1062)){throw new InvalidOperationException("A category with this name already exists. Choose another name.",ex);}
        catch(DbUpdateException ex)when(DatabaseError(ex,1451)||DatabaseError(ex,1452)){throw new InvalidOperationException("This category is referenced by another record. Deactivate it instead of deleting it.",ex);}
    }
    private async Task<object?> LockCategoryAsync(string kind,string id,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(id)||id.Length>20)return null;
        if(kind=="Income"){
            var row=(await db.IncomeCategories.FromSqlInterpolated($"SELECT * FROM `IncomeCategories` WHERE `IncomeCategoryId` = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            if(row!=null)await db.Entry(row).ReloadAsync(ct);return row;
        }
        var expense=(await db.ExpenseCategories.FromSqlInterpolated($"SELECT * FROM `ExpenseCategories` WHERE `ExpenseCategoryId` = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if(expense!=null)await db.Entry(expense).ReloadAsync(ct);return expense;
    }
    private static AdminCategoryRowViewModel Snapshot(object category)=>category switch {
        IncomeCategory c=>new(){CategoryId=c.IncomeCategoryId,CategoryName=c.CategoryName,Description=c.Description,DisplayOrder=c.DisplayOrder,IsActive=c.IsActive,CreatedDate=c.CreatedDate},
        ExpenseCategory c=>new(){CategoryId=c.ExpenseCategoryId,CategoryName=c.CategoryName,Description=c.Description,DisplayOrder=c.DisplayOrder,IsActive=c.IsActive,CreatedDate=c.CreatedDate},
        _=>throw new ArgumentException("Unknown category.")};
    public async Task<string> CreateAsync(string actor,string kind,AdminCategoryFormViewModel model,CancellationToken ct=default)
    {
        kind=ValidateKind(kind);var value=Validate(model);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        await AdminGuard.LockAsync(db,actor,ct);await UniqueAsync(kind,value.Name,null,ct);
        var id=await ids.GenerateIdAsync(kind=="Income"?"ICG":"ECG");
        if(kind=="Income")db.IncomeCategories.Add(new(){IncomeCategoryId=id,CategoryName=value.Name,Description=value.Description,DisplayOrder=model.DisplayOrder,IsActive=model.IsActive,CreatedDate=DateTime.UtcNow});
        else db.ExpenseCategories.Add(new(){ExpenseCategoryId=id,CategoryName=value.Name,Description=value.Description,DisplayOrder=model.DisplayOrder,IsActive=model.IsActive,CreatedDate=DateTime.UtcNow});
        await AdminGuard.AuditAsync(db,ids,actor,kind+"Category",id,"Create",$"Created {kind.ToLowerInvariant()} category '{value.Name}'.",ct);
        await SaveChangesAsync(ct);await tx.CommitAsync(ct);return id;
    }
    public async Task<bool> EditAsync(string actor,string kind,string id,AdminCategoryFormViewModel model,CancellationToken ct=default)
    {
        kind=ValidateKind(kind);var value=Validate(model);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        await AdminGuard.LockAsync(db,actor,ct);var category=await LockCategoryAsync(kind,id,ct);if(category==null)return false;
        var before=Snapshot(category);CheckStamp(before,model.EditStamp);
        if(Protected(kind,before.CategoryName)&&value.Name!=before.CategoryName)throw new InvalidOperationException("This system category's name must stay unchanged because financial services use its identity.");
        await UniqueAsync(kind,value.Name,id,ct);
        if(category is IncomeCategory income){income.CategoryName=value.Name;income.Description=value.Description;income.DisplayOrder=model.DisplayOrder;income.IsActive=model.IsActive;}
        else if(category is ExpenseCategory expense){expense.CategoryName=value.Name;expense.Description=value.Description;expense.DisplayOrder=model.DisplayOrder;expense.IsActive=model.IsActive;}
        await AdminGuard.AuditAsync(db,ids,actor,kind+"Category",id,"Edit",$"Updated {kind.ToLowerInvariant()} category '{value.Name}' (active: {model.IsActive}, order: {model.DisplayOrder}).",ct);
        await SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
    public async Task<bool> DeleteAsync(string actor,string kind,string id,string stamp,CancellationToken ct=default)
    {
        kind=ValidateKind(kind);await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        await AdminGuard.LockAsync(db,actor,ct);var category=await LockCategoryAsync(kind,id,ct);if(category==null)return false;
        var before=Snapshot(category);CheckStamp(before,stamp);
        if(Protected(kind,before.CategoryName))throw new InvalidOperationException("This system category must be retained. You may deactivate it instead.");
        var usage=await Rows(kind).Where(c=>c.CategoryId==id).Select(c=>c.UsageCount).SingleAsync(ct);
        if(usage>0)throw new InvalidOperationException("This category is used by existing records and cannot be deleted. Deactivate it instead.");
        if(category is IncomeCategory income)db.IncomeCategories.Remove(income);else db.ExpenseCategories.Remove((ExpenseCategory)category);
        await AdminGuard.AuditAsync(db,ids,actor,kind+"Category",id,"Delete",$"Deleted unused {kind.ToLowerInvariant()} category '{before.CategoryName}'.",ct);
        await SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
    public async Task<bool> DeactivateAsync(string actor,string kind,string id,string stamp,CancellationToken ct=default)
    {
        kind=ValidateKind(kind);await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
        await AdminGuard.LockAsync(db,actor,ct);var category=await LockCategoryAsync(kind,id,ct);if(category==null)return false;
        var before=Snapshot(category);CheckStamp(before,stamp);
        if(!before.IsActive)return true;
        if(category is IncomeCategory income)income.IsActive=false;else ((ExpenseCategory)category).IsActive=false;
        await AdminGuard.AuditAsync(db,ids,actor,kind+"Category",id,"Deactivate",$"Deactivated {kind.ToLowerInvariant()} category '{before.CategoryName}'; existing references retained.",ct);
        await SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
}
