# Create the first local administrator

This one-time interactive console command creates a new account with UserId `UID00000`, Role `Admin`, AccountStatus `Active`, and SecurityVersion `1`. It is deliberately separate from the web app: it is not a public endpoint, startup seeder, or migration seed.

1. Stop IPFMMS. Apply pending local migrations using `Update-Database` with IPFMMS selected in Visual Studio Package Manager Console. The Admin audit migration must be applied first.
2. Open Developer PowerShell and run:

   ```powershell
   dotnet run --project "C:\Users\User\source\repos\IPFMMS\tools\CreateFirstAdmin\CreateFirstAdmin.csproj"
   ```

   From the repository root, the portable equivalent is `dotnet run --project .\tools\CreateFirstAdmin\CreateFirstAdmin.csproj`.
3. Enter full name, email, optional phone, password, and confirmation. Password input is hidden. The password follows the normal registration policy: 8–100 characters with uppercase, lowercase, number, and special character.
4. Start IPFMMS and sign in using that email and password. Admin navigation is based on the saved Admin role.

The command reads the existing local DefaultConnection from app settings and the app assembly's existing User Secrets; it does not edit configuration or secrets files. Only localhost, 127.0.0.1 and ::1 are accepted, with an explicit database name. No remote/Azure connection is supported. Verify the displayed local database is the intended one before entering account details.

This is trusted local-owner provisioning: the administrator is created Active directly and no email-verification code is sent. The password is stored using the same ASP.NET PasswordHasher<User> as normal accounts. It never stores or prints the plain password. The creation and its BootstrapAdmin audit entry commit in one transaction.

Existing UID00000, an already-used email, or an existing active administrator causes the command to stop without overwriting/promoting any account. Normal UID numbering stays unchanged; do not reset IdCounters or rename another account's ID to UID00000. Subsequent administrator role changes can be performed through User Accounts by an active administrator.

Development validation: clean build and 17 isolated bootstrap checks covering host restrictions, password rules/hash compatibility, existing ID/email/admin protection, preserved UID counter/other users, atomic history and safe reruns. Only a disposable test database was used; no actual application account was created.
