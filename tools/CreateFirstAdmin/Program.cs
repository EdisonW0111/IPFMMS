using System.Text;
using IPFMMS.AdminSetup;
using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

if (args.Length > 0)
{
    Console.WriteLine("Run without arguments in an interactive terminal. This command creates only the first local Admin, UID00000.");
    return args.All(a => a is "--help" or "-h") ? 0 : 1;
}
if (Console.IsInputRedirected)
{
    Console.Error.WriteLine("Run this command in an interactive terminal so the password can be entered privately.");
    return 1;
}
try
{
    var directory = FindApplicationDirectory();
    var configuration = new ConfigurationBuilder().SetBasePath(directory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddUserSecrets(typeof(User).Assembly, optional: true).Build();
    var connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("No local DefaultConnection is configured in the app settings or existing User Secrets.");
    FirstAdminProvisioner.RequireLocalDatabase(connectionString);
    var connection = new MySqlConnectionStringBuilder(connectionString);
    Console.WriteLine("Create first local administrator: UID00000");
    Console.WriteLine($"Database: {connection.Database} on {connection.Server}:{connection.Port}");
    Console.WriteLine("This creates an active administrator directly for the local owner; it does not send an email verification code.");
    Console.WriteLine("Existing accounts, the UID counter, configuration files and secrets will remain unchanged.");
    var details = new RegisterViewModel {
        FullName = ReadText("Full name: "), Email = ReadText("Email address: "),
        PhoneNumber = ReadText("Phone number (optional): "),
        Password = ReadPassword("Password: "), ConfirmPassword = ReadPassword("Confirm password: ")
    };
    await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)).Options);
    await FirstAdminProvisioner.CreateAsync(db, details);
    details.Password = details.ConfirmPassword = string.Empty;
    Console.WriteLine("Created UID00000 with role Admin and status Active. Start the app and sign in with the email and password you entered.");
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("Setup cancelled. No account was created."); return 1; }
catch (InvalidOperationException error) { Console.Error.WriteLine(error.Message); return 1; }
catch (Exception) {
    Console.Error.WriteLine("Admin setup could not finish. Check that local MySQL is running, migrations are applied, and the configured account can insert Users, IdCounters and AdminAuditEntries. Connection strings/passwords are not printed.");
    return 1;
}

static string ReadText(string prompt)
{
    Console.Write(prompt);
    return Console.ReadLine() ?? throw new OperationCanceledException();
}
static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
        if (key.Key == ConsoleKey.Escape) throw new OperationCanceledException();
        if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
        else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
    }
}
static string FindApplicationDirectory()
{
    foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        for (var folder = new DirectoryInfo(start); folder != null; folder = folder.Parent)
            foreach (var candidate in new[] { folder.FullName, Path.Combine(folder.FullName, "IPFMMS") })
                if (File.Exists(Path.Combine(candidate, "IPFMMS.csproj"))) return candidate;
    throw new InvalidOperationException("Cannot locate IPFMMS.csproj. Run this command from your IPFMMS repository.");
}
