# Create first LIVE Admin UID00000

This separate local console tool generates private SQL offline. It does not read connection strings/User Secrets, connect to any database, migrate schema, or execute the SQL. It never starts automatically with the website. The original local CreateFirstAdmin helper remains local-only.

## Steps

1. Confirm the live Azure database has migration `20261109113958_AddAdminManagement`. Keep a current Azure backup.
2. In Developer PowerShell run:

   ```powershell
   dotnet run --project "C:\Users\User\source\repos\IPFMMS\tools\CreateLiveAdmin\CreateLiveAdmin.csproj" -- --output "D:\Downloads\IPFMMS_Create_Live_Admin.sql"
   ```

3. Enter full name, an email not already registered in the LIVE system, optional phone, password and confirmation. Password entry is hidden. The regular registration password policy applies (8–100 characters, uppercase, lowercase, number, special character).
4. Wait for the Saved message. Keep the resulting SQL private: it contains the password hash. Do not commit it or paste it into chat. The tool refuses existing output files and output inside the application Git repository.
5. In MySQL Workbench open the existing Azure MySQL connection, then File > Open SQL Script and choose `D:\Downloads\IPFMMS_Create_Live_Admin.sql`. Run the entire file.
6. Verify the returned row shows UserId UID00000, Role Admin, AccountStatus Active. Sign in to the LIVE website using the email/password entered in PowerShell. The email is the login name; UID00000 is the internal identifier.

The SQL requires the Admin migration, checks UID00000/email absence and absence of another active Admin, and creates one Active Admin plus one audit record in a transaction. No OTP email is sent for this trusted owner bootstrap. Existing accounts, financial data and the UID numbering counter remain unchanged. Identity strings use UTF-8 hexadecimal SQL literals, and passwords use the same ASP.NET PasswordHasher<User> as the website. A SQL error rolls back account/audit writes. Rerunning after success refuses to overwrite the account; choose password recovery if its password is later forgotten.

If UID00000 or the chosen email already exists, or an active Admin exists, stop and inspect the returned error; this helper is not an account-renaming or overwrite tool. If the output path already exists, use another private filename instead of rerunning over it.
