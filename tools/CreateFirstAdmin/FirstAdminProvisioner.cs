using System.ComponentModel.DataAnnotations;
using System.Data;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace IPFMMS.AdminSetup;

public static class FirstAdminProvisioner
{
    public const string AdminId = "UID00000";

    public static void RequireLocalDatabase(string connectionString)
    {
        var connection = new MySqlConnectionStringBuilder(connectionString);
        if (!new[] { "localhost", "127.0.0.1", "::1" }.Contains(connection.Server, StringComparer.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(connection.Database))
            throw new InvalidOperationException("This setup command requires a named local MySQL database. Azure/remote servers are not supported.");
    }

    public static async Task CreateAsync(ApplicationDbContext db, RegisterViewModel details)
    {
        RequireLocalDatabase(db.Database.GetConnectionString() ?? "");
        details.FullName = details.FullName.Trim();
        details.Email = details.Email.Trim().ToLowerInvariant();
        details.PhoneNumber = string.IsNullOrWhiteSpace(details.PhoneNumber) ? null : details.PhoneNumber.Trim();
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(details, new ValidationContext(details), errors, true))
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(e => e.ErrorMessage)));
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Apply the pending local migrations with Update-Database before running Admin setup.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.Users.AnyAsync(u => u.UserId == AdminId))
            throw new InvalidOperationException("UID00000 already exists. This command never overwrites or promotes an existing account.");
        if (await db.Users.AnyAsync(u => u.Email == details.Email))
            throw new InvalidOperationException("That email already belongs to an account. Use a different email for this new administrator.");
        if (await db.Users.AnyAsync(u => u.Role == "Admin" && u.AccountStatus == AccountStatus.Active))
            throw new InvalidOperationException("An active administrator already exists. This command is only for preparing the first administrator.");

        var user = new User {
            UserId = AdminId, FullName = details.FullName, Email = details.Email,
            PhoneNumber = details.PhoneNumber, Role = "Admin", AccountStatus = AccountStatus.Active,
            SecurityVersion = 1, CreatedDate = DateTime.UtcNow
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, details.Password);
        db.Users.Add(user);
        db.AdminAuditEntries.Add(new AdminAuditEntry {
            AuditId = await new IdGeneratorService(db).GenerateIdAsync("AUD"),
            AdminUserId = AdminId, EntityType = "User", EntityId = AdminId,
            Action = "BootstrapAdmin", Summary = "First administrator initialized by the local database owner.",
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
