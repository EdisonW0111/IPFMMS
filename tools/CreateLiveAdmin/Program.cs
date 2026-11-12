using System.Text;
using IPFMMS.LiveAdminSetup;
using IPFMMS.ViewModels;
if(args.Length==1&&args[0]=="--help") {Console.WriteLine("Generate offline SQL: dotnet run --project <CreateLiveAdmin.csproj> -- --output <absolute-private-file.sql>. No database connection or execution.");return 0;}
if(args.Length!=2||args[0]!="--output"||!Path.IsPathFullyQualified(args[1])||Console.IsInputRedirected){Console.Error.WriteLine("Use an interactive terminal with --output and an absolute SQL file path.");return 1;}
try {
    var output=Path.GetFullPath(args[1]);
    if(!string.Equals(Path.GetExtension(output),".sql",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Choose a .sql output file.");
    for(var folder=new DirectoryInfo(AppContext.BaseDirectory);folder!=null;folder=folder.Parent)
        if(Directory.Exists(Path.Combine(folder.FullName,".git"))||File.Exists(Path.Combine(folder.FullName,".git"))) {
            if(output.StartsWith(folder.FullName+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Choose an output outside the application Git repository. The SQL contains a password hash.");
            break;
        }
    if(File.Exists(output))throw new InvalidOperationException("Output file already exists. Choose a new filename; nothing was overwritten.");
    Console.WriteLine("Generate SQL for LIVE Admin UID00000. No database is connected or changed.");
    Console.WriteLine("Use a new email not already registered in the LIVE system. Passwords are hidden while typing.");
    var input=new RegisterViewModel{FullName=Read("Full name: "),Email=Read("Email address: "),PhoneNumber=Read("Phone (optional): "),Password=Secret("Password: "),ConfirmPassword=Secret("Confirm password: ")};
    var sql=LiveAdminSql.Generate(input);input.Password=input.ConfirmPassword=string.Empty;
    await using(var file=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None))
    await using(var writer=new StreamWriter(file,new UTF8Encoding(false))) await writer.WriteAsync(sql);
    Console.WriteLine("Saved private SQL file: "+output);
    Console.WriteLine("Nothing has been applied. In Workbench, use the Azure connection, run this entire file, and check UID00000 / Admin / Active before logging in.");
    return 0;
} catch(OperationCanceledException){Console.Error.WriteLine("Cancelled. No database was changed.");return 1;}
catch(InvalidOperationException ex){Console.Error.WriteLine(ex.Message);return 1;}
catch(Exception){Console.Error.WriteLine("Could not generate the SQL file. Check the output directory/access. No database was changed.");return 1;}
static string Read(string prompt){Console.Write(prompt);return Console.ReadLine()??throw new OperationCanceledException();}
static string Secret(string prompt){Console.Write(prompt);var value=new StringBuilder();while(true){var key=Console.ReadKey(true);if(key.Key==ConsoleKey.Enter){Console.WriteLine();return value.ToString();}if(key.Key==ConsoleKey.Escape)throw new OperationCanceledException();if(key.Key==ConsoleKey.Backspace){if(value.Length>0)value.Length--;}else if(!char.IsControl(key.KeyChar))value.Append(key.KeyChar);}}
