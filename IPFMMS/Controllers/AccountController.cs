using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace IPFMMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IdGeneratorService _idGenerator;
        private readonly EmailService _emailService;
        private readonly OtpService _otpService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly TurnstileService _turnstileService;
        private readonly LoginAttemptService _loginAttemptService;
        private readonly PasswordResetTokenService _passwordResetTokenService;

        public AccountController(
            ApplicationDbContext context,
            IdGeneratorService idGenerator,
            EmailService emailService,
            OtpService otpService,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration,
            TurnstileService turnstileService,
            LoginAttemptService loginAttemptService,
            PasswordResetTokenService passwordResetTokenService)
        {
            _context = context;
            _idGenerator = idGenerator;
            _emailService = emailService;
            _otpService = otpService;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _turnstileService = turnstileService;
            _loginAttemptService = loginAttemptService; 
            _passwordResetTokenService = passwordResetTokenService;
        }

        private async Task<User?> LockUserByIdAsync(string userId)
        {
            if (_context.Database.CurrentTransaction == null)
                throw new InvalidOperationException("An account transaction is required.");
            var users = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM Users WHERE UserId = {userId} FOR UPDATE")
                .AsTracking().ToListAsync();
            var user = users.SingleOrDefault();
            if (user != null) await _context.Entry(user).ReloadAsync();
            return user;
        }

        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.TurnstileSiteKey = _configuration["Turnstile:SiteKey"];

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel model, 
            [FromForm(Name = "cf-turnstile-response")] string? turnstileToken)
        {
            // 1. Validate registration details.
            if (!ModelState.IsValid)
            {
                ViewBag.TurnstileSiteKey = _configuration["Turnstile:SiteKey"];

                return View(model);
            }

            // Verify Cloudflare Turnstile before modifying account data.
            bool captchaValid =
                await _turnstileService.VerifyTokenAsync(turnstileToken);

            if (!captchaValid)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "CAPTCHA verification failed. Please try again.");

                ViewBag.TurnstileSiteKey =
                    _configuration["Turnstile:SiteKey"];

                return View(model);
            }

            string email = model.Email.Trim().ToLowerInvariant();
            DateTime now = DateTime.UtcNow;

            // 2. Generate OTP.
            string otpCode = _otpService.GenerateOtp();

            // 3. Begin transaction to protect account and OTP updates.
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // Lock an existing account during registration updates.
                var users = await _context.Users
                    .FromSqlInterpolated(
                        $@"SELECT * FROM Users
                   WHERE Email = {email}
                   FOR UPDATE")
                    .AsTracking()
                    .ToListAsync();

                var user = users.FirstOrDefault();
                if (user != null) await _context.Entry(user).ReloadAsync();

                if (user != null && user.AccountStatus != "Pending")
                {
                    await transaction.RollbackAsync();

                    ModelState.AddModelError(
                        nameof(model.Email),
                        "This email address is already registered " +
                        "or unavailable for registration.");

                    return View(model);
                }

                string userId;

                if (user == null)
                {
                    // 4A. Create a new Pending account.
                    userId = await _idGenerator.GenerateIdAsync("UID");

                    user = new User
                    {
                        UserId = userId,
                        FullName = model.FullName.Trim(),
                        Email = email,
                        PhoneNumber = model.PhoneNumber?.Trim(),
                        Role = "User",
                        AccountStatus = "Pending",
                        CreatedDate = now
                    };

                    user.PasswordHash = _passwordHasher.HashPassword(
                        user, model.Password);

                    _context.Users.Add(user);
                }
                else
                {
                    // 4B. Existing Pending account.
                    userId = user.UserId;

                    // Enforce OTP cooldown and hourly limit.
                    var recentOtps = await _context.EmailOTPs
                        .Where(o =>
                            o.UserId == userId &&
                            o.Purpose == EmailOtpPurpose.Registration &&
                            o.CreatedDateTime >= now.AddHours(-1))
                        .OrderByDescending(o => o.CreatedDateTime)
                        .ToListAsync();

                    if (recentOtps.Count > 0 &&
                        recentOtps[0].CreatedDateTime > now.AddSeconds(-60))
                    {
                        await transaction.RollbackAsync();

                        ModelState.AddModelError(
                            string.Empty,
                            "Please wait 60 seconds before " +
                            "requesting another verification code.");

                        return View(model);
                    }

                    if (recentOtps.Count >= 6)
                    {
                        await transaction.RollbackAsync();

                        ModelState.AddModelError(
                            string.Empty,
                            "Too many verification requests. " +
                            "Please try again later.");

                        return View(model);
                    }

                    // Update information immediately.
                    user.FullName = model.FullName.Trim();
                    user.PhoneNumber = model.PhoneNumber?.Trim();

                    user.PasswordHash = _passwordHasher.HashPassword(
                        user, model.Password);

                    // Invalidate previous unverified registration OTPs.
                    var oldOtps = await _context.EmailOTPs
                        .Where(o =>
                            o.UserId == userId &&
                            o.Purpose == EmailOtpPurpose.Registration &&
                            !o.IsVerified)
                        .ToListAsync();

                    foreach (var oldOtp in oldOtps)
                    {
                        oldOtp.ExpiryDateTime = now;
                    }
                }

                // 5. Create the new OTP record.
                string otpId = await _idGenerator.GenerateIdAsync("OTP");

                var emailOtp = new EmailOTP
                {
                    OtpId = otpId,
                    UserId = userId,
                    OtpCode = _otpService.HashOtp(userId, otpCode),
                    Purpose = EmailOtpPurpose.Registration,
                    CreatedDateTime = now,
                    ExpiryDateTime = now.AddMinutes(5),
                    IsVerified = false,
                    FailedAttempts = 0
                };

                _context.EmailOTPs.Add(emailOtp);

                // 6. Commit account and OTP changes together.
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 7. Send verification email.
                try
                {
                    string subject = "IPFMMS - Email Verification Code";

                    string body =
                        $"Hello {user.FullName},\n\n" +
                        "Thank you for registering with IPFMMS.\n\n" +
                        $"Your verification code is: {otpCode}\n\n" +
                        "This code expires in 5 minutes.\n\n" +
                        "If you did not request this, please ignore this email.\n\n" +
                        "IPFMMS Team";

                    await _emailService.SendEmailAsync(
                        email, subject, body);
                }
                catch (Exception)
                {
                    TempData["OtpNotice"] =
                        "Your registration details were saved, " +
                        "but the verification email could not be sent. " +
                        "Please request another code after the cooldown.";
                }

                return RedirectToAction(
                    nameof(VerifyOtp),
                    new { userId });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "Registration could not be completed. " +
                    "Please check your details and try again.");

                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> VerifyOtp(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.UserId == userId &&
                    u.AccountStatus == AccountStatus.Pending);

            if (user == null)
                return NotFound();

            var model = new VerifyOtpViewModel
            {
                UserId = user.UserId
            };

            await PopulateOtpViewModelAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(
            VerifyOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateOtpViewModelAsync(model);
                return View(model);
            }

            // Lock the account before its OTP. Administrative access changes use
            // the same order, so a deleted account cannot be activated from a stale read.
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var user = await LockUserByIdAsync(model.UserId);

            if (user == null || user.AccountStatus != "Pending")
            {
                await transaction.RollbackAsync();
                return NotFound();
            }

            model.UserId = user.UserId;
            model.Email = user.Email;

            var otpRecords = await _context.EmailOTPs
                .FromSqlInterpolated(
                    $@"SELECT * FROM EmailOTPs
               WHERE UserId = {model.UserId}
                 AND Purpose = {EmailOtpPurpose.Registration}
                 AND IsVerified = FALSE
               ORDER BY CreatedDateTime DESC, OtpId DESC
               LIMIT 1 FOR UPDATE")
                .AsTracking()
                .ToListAsync();

            var otp = otpRecords.FirstOrDefault();
            if (otp != null) await _context.Entry(otp).ReloadAsync();

            if (otp == null)
            {
                ModelState.AddModelError(
                    nameof(model.OtpCode),
                    "No active verification code was found.");

                await transaction.RollbackAsync();
                await PopulateOtpViewModelAsync(model);
                return View(model);
            }

            if (otp.ExpiryDateTime <= DateTime.UtcNow)
            {
                ModelState.AddModelError(
                    nameof(model.OtpCode),
                    "Your verification code has expired.");

                await transaction.RollbackAsync();
                await PopulateOtpViewModelAsync(model);
                return View(model);
            }

            if (otp.FailedAttempts >= 5)
            {
                ModelState.AddModelError(
                    nameof(model.OtpCode),
                    "Too many incorrect attempts. This code is locked.");

                await transaction.RollbackAsync();
                await PopulateOtpViewModelAsync(model);
                return View(model);
            }

            bool isCorrect = _otpService.VerifyOtp(
                user.UserId,
                model.OtpCode,
                otp.OtpCode);

            if (!isCorrect)
            {
                otp.FailedAttempts++;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                ModelState.AddModelError(
                    nameof(model.OtpCode),
                    otp.FailedAttempts >= 5
                        ? "Too many incorrect attempts. This code is locked."
                        : "Incorrect verification code.");

                await PopulateOtpViewModelAsync(model);

                return View(model);
            }

            // OTP is correct: activate the account.
            otp.IsVerified = true;
            user.AccountStatus = AccountStatus.Active;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Your email has been verified successfully.";

            return RedirectToAction(nameof(VerificationSuccess));
        }

        [HttpGet]
        public IActionResult VerificationSuccess()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest();

            // Begin transaction before checking the user.
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Lock the pending user row to serialize resend requests.
            var users = await _context.Users
                .FromSqlInterpolated(
                    $@"SELECT * FROM Users
               WHERE UserId = {userId}
               FOR UPDATE")
                .AsTracking()
                .ToListAsync();

            var user = users.FirstOrDefault();
            if (user != null) await _context.Entry(user).ReloadAsync();

            if (user == null || user.AccountStatus != "Pending")
            {
                await transaction.RollbackAsync();
                return NotFound();
            }

            userId = user.UserId;
            DateTime now = DateTime.UtcNow;

            var recentOtps = await _context.EmailOTPs
                .Where(o => o.UserId == userId &&
                            o.Purpose == EmailOtpPurpose.Registration &&
                            o.CreatedDateTime >= now.AddHours(-1))
                .OrderByDescending(o => o.CreatedDateTime)
                .ToListAsync();

            // Prevent resending within 60 seconds.
            if (recentOtps.Any() &&
                recentOtps[0].CreatedDateTime >
                now.AddSeconds(-60))
            {
                await transaction.RollbackAsync();

                TempData["OtpNotice"] =
                    "Please wait 60 seconds before requesting another code.";

                return RedirectToAction(
                    nameof(VerifyOtp), new { userId });
            }

            // Initial OTP + maximum five resends in one hour.
            if (recentOtps.Count >= 6)
            {
                await transaction.RollbackAsync();

                TempData["OtpNotice"] =
                    "Too many OTP requests. Please try again later.";

                return RedirectToAction(
                    nameof(VerifyOtp), new { userId });
            }

            // Invalidate all previous registration OTPs.
            var oldOtps = await _context.EmailOTPs
                .Where(o => o.UserId == userId &&
                            o.Purpose == EmailOtpPurpose.Registration &&
                            !o.IsVerified)
                .ToListAsync();

            foreach (var oldOtp in oldOtps)
            {
                oldOtp.ExpiryDateTime = now;
            }

            string otpCode = _otpService.GenerateOtp();
            string otpId = await _idGenerator.GenerateIdAsync("OTP");

            var newOtp = new EmailOTP
            {
                OtpId = otpId,
                UserId = userId,
                OtpCode = _otpService.HashOtp(userId, otpCode),
                Purpose = EmailOtpPurpose.Registration,
                CreatedDateTime = now,
                ExpiryDateTime = now.AddMinutes(5),
                IsVerified = false,
                FailedAttempts = 0
            };

            _context.EmailOTPs.Add(newOtp);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            try
            {
                await _emailService.SendEmailAsync(
                    user.Email,
                    "IPFMMS - New Verification Code",
                    $"Hello {user.FullName},\n\n" +
                    $"Your new verification code is: {otpCode}\n\n" +
                    "This code expires in 5 minutes.\n\n" +
                    "If you did not request this email, please ignore it.\n\n" +
                    "IPFMMS Team");

                TempData["OtpNotice"] =
                    "A new verification code has been sent.";
            }
            catch (Exception)
            {
                TempData["OtpNotice"] =
                    "We could not send the verification email. " +
                    "Please try again after the cooldown.";
            }

            return RedirectToAction(
                nameof(VerifyOtp), new { userId });
        }

        private async Task PopulateOtpViewModelAsync(VerifyOtpViewModel model)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == model.UserId);

            if (user == null)
                return;

            model.Email = user.Email;

            var latestOtp = await _context.EmailOTPs
                .AsNoTracking()
                .Where(o =>
                    o.UserId == model.UserId &&
                    o.Purpose == EmailOtpPurpose.Registration)
                .OrderByDescending(o => o.CreatedDateTime)
                .ThenByDescending(o => o.OtpId)
                .FirstOrDefaultAsync();

            DateTime? resendAvailableAt =
                latestOtp?.CreatedDateTime.AddSeconds(60);

            model.ResendAvailableAtUtc = resendAvailableAt;

            model.ResendCooldownSeconds =
                resendAvailableAt.HasValue
                    ? Math.Max(
                        0,
                        (int)Math.Ceiling(
                            (resendAvailableAt.Value - DateTime.UtcNow)
                            .TotalSeconds))
                    : 0;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.TurnstileSiteKey =
                _configuration["Turnstile:SiteKey"];

            return View(new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
    LoginViewModel model,
    [FromForm(Name = "cf-turnstile-response")] string? turnstileToken)
        {
            ViewBag.TurnstileSiteKey =
                _configuration["Turnstile:SiteKey"];

            if (!ModelState.IsValid)
                return View(model);

            string email = model.Email.Trim().ToLowerInvariant();

            // 1. Check temporary lockout.
            TimeSpan remainingLockout =
                _loginAttemptService.GetRemainingLockout(email);

            if (remainingLockout > TimeSpan.Zero)
            {
                ModelState.AddModelError(
                    string.Empty,
                    $"Too many unsuccessful login attempts. " +
                    $"Please try again in " +
                    $"{Math.Ceiling(remainingLockout.TotalSeconds)} seconds.");

                return View(model);
            }

            // 2. Verify Cloudflare Turnstile.
            bool captchaValid =
                await _turnstileService.VerifyTokenAsync(turnstileToken);

            if (!captchaValid)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "CAPTCHA verification failed. Please try again.");

                return View(model);
            }

            // 3. Find account.
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                _loginAttemptService.RecordFailedAttempt(email);

                ModelState.AddModelError(
                    string.Empty,
                    "Invalid email address or password.");

                return View(model);
            }

            // 4. Verify password hash.
            var passwordResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                model.Password);

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                _loginAttemptService.RecordFailedAttempt(email);

                ModelState.AddModelError(
                    string.Empty,
                    "Invalid email address or password.");

                return View(model);
            }

            // 5. Enforce account status.
            if (!AccountStatus.CanLogin(user.AccountStatus))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your account is not active. " +
                    "Please complete email verification or contact support.");

                return View(model);
            }

            // 6. Clear failed attempts after successful authentication.
            _loginAttemptService.ClearAttempts(email);

            // 7. Create authentication claims.
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.UserId),
        new Claim(ClaimTypes.Name, user.FullName),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role),

        // Used to invalidate outdated login sessions.
        new Claim("SecurityVersion", user.SecurityVersion.ToString())
    };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            // 8. Configure Remember Me.
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,

                ExpiresUtc = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddMinutes(30),

                AllowRefresh = !model.RememberMe
            };

            // 9. Sign in.
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProperties);

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return Content(
                "Access denied. You do not have permission to view this page.");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [Authorize(Roles = "User,Parent,Admin")]
        public async Task<IActionResult> UpdateProfile()
        {
            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound();

            var model = new UpdateProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "User,Parent,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(
            UpdateProfileViewModel model)
        {
            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var user = await LockUserByIdAsync(userId);

            if (user == null)
                return NotFound();
            if (!AccountStatus.CanLogin(user.AccountStatus))
                return Forbid();

            // The email field is displayed only.
            // Never trust a submitted email address.
            ModelState.Remove(nameof(model.Email));
            model.Email = user.Email;

            if (!ModelState.IsValid)
                return View(model);

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber)
                ? null
                : model.PhoneNumber.Trim();

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Refresh the username stored in the authentication cookie.
            var currentAuth = await HttpContext.AuthenticateAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            if (currentAuth.Succeeded &&
                currentAuth.Principal != null &&
                currentAuth.Properties != null)
            {
                var identity = currentAuth.Principal.Identity
                    as ClaimsIdentity;

                if (identity != null)
                {
                    // Remove the old name claim.
                    var oldNameClaim = identity.FindFirst(ClaimTypes.Name);

                    if (oldNameClaim != null)
                    {
                        identity.RemoveClaim(oldNameClaim);
                    }

                    // Add the updated name.
                    identity.AddClaim(
                        new Claim(ClaimTypes.Name, user.FullName));

                    // Preserve the existing login expiration and Remember Me.
                    var authProperties = currentAuth.Properties;

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        currentAuth.Principal,
                        authProperties);
                }
            }

            TempData["SuccessMessage"] =
                "Your profile has been updated successfully.";

            return RedirectToAction(nameof(UpdateProfile));
        }

        [HttpGet]
        [Authorize(Roles = "User,Parent,Admin")]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "User,Parent,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            ChangePasswordViewModel model)
        {
            // 1. Validate form inputs.
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 2. Identify the logged-in user.
            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // 3. Lock and reload the account before checking access and changing
            // PasswordHash/SecurityVersion, preserving concurrent administrative changes.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var user = await LockUserByIdAsync(userId);

            if (user == null ||
                !AccountStatus.CanLogin(user.AccountStatus))
            {
                return Forbid();
            }

            // 4. Verify current password.
            var currentPasswordResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    model.CurrentPassword);

            if (currentPasswordResult ==
                PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    nameof(model.CurrentPassword),
                    "Current password is incorrect.");

                return View(model);
            }

            // 5. Prevent reusing the current password.
            var newPasswordResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    model.NewPassword);

            if (newPasswordResult !=
                PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    nameof(model.NewPassword),
                    "New password must be different from your current password.");

                return View(model);
            }

            // 6. Generate secure password hash.
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                model.NewPassword);

            user.SecurityVersion++;

            // 7. Save new password to MySQL.
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Retrieve the current authentication ticket.
            var currentAuth = await HttpContext.AuthenticateAsync(
     CookieAuthenticationDefaults.AuthenticationScheme);

            if (!currentAuth.Succeeded ||
                currentAuth.Principal == null ||
                currentAuth.Properties == null ||
                currentAuth.Principal.Identity is not ClaimsIdentity identity)
            {
                // Password was changed, but the current session
                // could not be refreshed safely.
                await HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return RedirectToAction(nameof(Login));
            }

            // Remove the previous security-version claim.
            var oldVersionClaim = identity.FindFirst("SecurityVersion");

            if (oldVersionClaim != null)
            {
                identity.RemoveClaim(oldVersionClaim);
            }

            // Add the latest security version from MySQL.
            identity.AddClaim(
                new Claim("SecurityVersion", user.SecurityVersion.ToString()));

            // Preserve the current ticket properties, including
            // Remember Me and the original expiration.
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                currentAuth.Principal,
                currentAuth.Properties);

            TempData["SuccessMessage"] =
                "Your password has been changed successfully.";

            return RedirectToAction(nameof(ChangePassword));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting("PasswordRecovery")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
    ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string email = model.Email.Trim().ToLowerInvariant();
            TempData["ResetEmail"] = email;
            TempData["ResetRequestTime"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            DateTime now = DateTime.UtcNow;

            // Use the same response for existing and unknown emails.
            const string confirmationMessage =
                "If this email is registered with an active account, " +
                "a password reset verification code will be sent.";

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Lock the user row to serialize OTP requests.
            var users = await _context.Users
                .FromSqlInterpolated(
                    $@"SELECT * FROM Users
               WHERE Email = {email}
               FOR UPDATE")
                .AsTracking()
                .ToListAsync();

            var user = users.FirstOrDefault();
            if (user != null) await _context.Entry(user).ReloadAsync();

            // Don't reveal whether the email is registered.
            if (user == null ||
                !AccountStatus.CanLogin(user.AccountStatus))
            {
                await transaction.RollbackAsync();

                TempData["ResetNotice"] = confirmationMessage;

                return RedirectToAction(nameof(VerifyResetOtp));
            }

            var recentOtps = await _context.EmailOTPs
                .Where(o =>
                    o.UserId == user.UserId &&
                    o.Purpose == EmailOtpPurpose.PasswordReset &&
                    o.CreatedDateTime >= now.AddHours(-1))
                .OrderByDescending(o => o.CreatedDateTime)
                .ToListAsync();

            // Enforce the 60-second cooldown.
            if (recentOtps.Count > 0 &&
                recentOtps[0].CreatedDateTime > now.AddSeconds(-60))
            {
                await transaction.RollbackAsync();

                TempData["ResetNotice"] = confirmationMessage;

                return RedirectToAction(nameof(VerifyResetOtp));
            }

            // Maximum 6 password-reset OTP requests per hour.
            if (recentOtps.Count >= 6)
            {
                await transaction.RollbackAsync();

                TempData["ResetNotice"] = confirmationMessage;

                return RedirectToAction(nameof(VerifyResetOtp));
            }

            // Invalidate previous unused password-reset OTPs.
            var oldOtps = await _context.EmailOTPs
                .Where(o =>
                    o.UserId == user.UserId &&
                    o.Purpose == EmailOtpPurpose.PasswordReset &&
                    !o.IsVerified)
                .ToListAsync();

            foreach (var oldOtp in oldOtps)
            {
                oldOtp.ExpiryDateTime = now;
            }

            // Generate a new six-digit OTP.
            string otpCode = _otpService.GenerateOtp();

            string otpId =
                await _idGenerator.GenerateIdAsync("OTP");

            var emailOtp = new EmailOTP
            {
                OtpId = otpId,
                UserId = user.UserId,
                OtpCode = _otpService.HashOtp(
                    user.UserId, otpCode),
                Purpose = EmailOtpPurpose.PasswordReset,
                CreatedDateTime = now,
                ExpiryDateTime = now.AddMinutes(5),
                IsVerified = false,
                FailedAttempts = 0
            };

            _context.EmailOTPs.Add(emailOtp);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Send the password-reset OTP.
            try
            {
                string subject = "IPFMMS - Password Reset Code";

                string body =
                    $"Hello {user.FullName},\n\n" +
                    "We received a request to reset your IPFMMS password.\n\n" +
                    $"Your verification code is: {otpCode}\n\n" +
                    "This code expires in 5 minutes.\n\n" +
                    "If you did not request this, please ignore this email.\n\n" +
                    "IPFMMS Team";

                await _emailService.SendEmailAsync(
                    user.Email, subject, body);
            }
            catch (Exception)
            {
                
            }

            TempData["ResetNotice"] = confirmationMessage;

            return RedirectToAction(nameof(VerifyResetOtp));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyResetOtp()
        {
            var model = new ResetPasswordOtpViewModel
            {
                Email = TempData.Peek("ResetEmail")?.ToString()
                    ?? string.Empty
            };

            await PopulateResetOtpViewModelAsync(model);

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting("PasswordRecovery")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyResetOtp(
    ResetPasswordOtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateResetOtpViewModelAsync(model);
                return View(model);
            }

            string email = TempData.Peek("ResetEmail")?
    .ToString()?.Trim().ToLowerInvariant() ?? "";

            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            model.Email = email;

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var users = await _context.Users
                .FromSqlInterpolated($"SELECT * FROM Users WHERE Email = {email} FOR UPDATE")
                .AsTracking().ToListAsync();
            var user = users.SingleOrDefault();
            if (user != null) await _context.Entry(user).ReloadAsync();

            if (user == null || !AccountStatus.CanLogin(user.AccountStatus))
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    nameof(model.OtpCode),
                    "Invalid or expired verification code.");

                await PopulateResetOtpViewModelAsync(model);
                return View(model);
            }

            // Lock the most recent unused password-reset OTP.
            var otpRecords = await _context.EmailOTPs
                .FromSqlInterpolated(
                    $@"SELECT * FROM EmailOTPs
               WHERE UserId = {user.UserId}
                 AND Purpose = {EmailOtpPurpose.PasswordReset}
                 AND IsVerified = FALSE
               ORDER BY CreatedDateTime DESC, OtpId DESC
               LIMIT 1 FOR UPDATE")
                .AsTracking()
                .ToListAsync();

            var otp = otpRecords.FirstOrDefault();
            if (otp != null) await _context.Entry(otp).ReloadAsync();

            if (otp == null ||
                otp.ExpiryDateTime <= DateTime.UtcNow ||
                otp.FailedAttempts >= 5)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    nameof(model.OtpCode),
                    "Invalid or expired verification code.");

                await PopulateResetOtpViewModelAsync(model);
                return View(model);
            }

            bool isCorrect = _otpService.VerifyOtp(
                user.UserId,
                model.OtpCode,
                otp.OtpCode);

            if (!isCorrect)
            {
                otp.FailedAttempts++;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                ModelState.AddModelError(
                    nameof(model.OtpCode),
                    "Invalid or expired verification code.");

                await PopulateResetOtpViewModelAsync(model);
                return View(model);
            }

            // 1. Generate a secure reset token.
            string resetToken =
                _passwordResetTokenService.GenerateToken();

            string tokenHash =
                _passwordResetTokenService.HashToken(resetToken);

            DateTime now = DateTime.UtcNow;

            // 2. Invalidate previous unused reset tokens
            // for this user.
            var oldTokens = await _context.PasswordResetTokens
                .Where(t =>
                    t.UserId == user.UserId &&
                    !t.IsUsed)
                .ToListAsync();

            foreach (var oldToken in oldTokens)
            {
                oldToken.IsUsed = true;
            }

            // 3. Create a new reset authorization.
            var passwordResetToken = new PasswordResetToken
            {
                UserId = user.UserId,
                TokenHash = tokenHash,
                CreatedDateTime = now,
                ExpiryDateTime = now.AddMinutes(10),
                IsUsed = false
            };

            _context.PasswordResetTokens.Add(passwordResetToken);

            // 4. Mark the OTP as verified.
            // These changes are saved in the same transaction.
            otp.IsVerified = true;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 5. Store the actual token in a secure browser cookie.
            // Never store the plaintext token in MySQL.
            Response.Cookies.Append(
                "IPFMMS.PasswordReset",
                resetToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    IsEssential = true,
                    Path = "/Account",
                    Expires = DateTimeOffset.UtcNow.AddMinutes(10)
                });

            // 6. Continue to the Reset Password page.
            return RedirectToAction(nameof(ResetPassword));
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword()
        {
            string? resetToken =
                Request.Cookies["IPFMMS.PasswordReset"];

            if (string.IsNullOrWhiteSpace(resetToken))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            string tokenHash =
                _passwordResetTokenService.HashToken(resetToken);

            var tokenRecord = await _context.PasswordResetTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.TokenHash == tokenHash &&
                    !t.IsUsed &&
                    t.ExpiryDateTime > DateTime.UtcNow);

            if (tokenRecord == null)
            {
                Response.Cookies.Delete(
                    "IPFMMS.PasswordReset",
                    new CookieOptions { Path = "/Account" });

                TempData["ResetNotice"] =
                    "Your password reset session has expired. " +
                    "Please request a new verification code.";

                return RedirectToAction(nameof(ForgotPassword));
            }

            return View(new ResetPasswordViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting("PasswordRecovery")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
    ResetPasswordViewModel model)
        {
            string? resetToken =
                Request.Cookies["IPFMMS.PasswordReset"];

            if (string.IsNullOrWhiteSpace(resetToken))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            string tokenHash =
                _passwordResetTokenService.HashToken(resetToken);

            // Validate the reset authorization independently
            // of the form's input.
            if (!ModelState.IsValid)
            {
                bool tokenIsValid =
                    await _context.PasswordResetTokens
                        .AsNoTracking()
                        .AnyAsync(t =>
                            t.TokenHash == tokenHash &&
                            !t.IsUsed &&
                            t.ExpiryDateTime > DateTime.UtcNow);

                if (!tokenIsValid)
                {
                    Response.Cookies.Delete(
                        "IPFMMS.PasswordReset",
                        new CookieOptions { Path = "/Account" });

                    return RedirectToAction(nameof(ForgotPassword));
                }

                return View(model);
            }

            // Resolve the token's owner without locking it, then lock the account
            // first. The locked token below is rechecked before any credential change.
            var tokenOwner = await _context.PasswordResetTokens.AsNoTracking()
                .Where(t => t.TokenHash == tokenHash).Select(t => t.UserId).SingleOrDefaultAsync();
            if (tokenOwner == null)
            {
                Response.Cookies.Delete("IPFMMS.PasswordReset", new CookieOptions { Path = "/Account" });
                TempData["ResetNotice"] = "Your password reset session is invalid or expired.";
                return RedirectToAction(nameof(ForgotPassword));
            }
            await using var transaction =
                await _context.Database.BeginTransactionAsync();
            var user = await LockUserByIdAsync(tokenOwner);
            if (user == null || !AccountStatus.CanLogin(user.AccountStatus))
            {
                await transaction.RollbackAsync();
                Response.Cookies.Delete("IPFMMS.PasswordReset", new CookieOptions { Path = "/Account" });
                return RedirectToAction(nameof(ForgotPassword));
            }

            // Lock the token row so simultaneous submissions
            // cannot consume the same token twice.
            var tokenRecords = await _context.PasswordResetTokens
                .FromSqlInterpolated(
                    $@"SELECT * FROM PasswordResetTokens
               WHERE TokenHash = {tokenHash}
               LIMIT 1 FOR UPDATE")
                .AsTracking()
                .ToListAsync();

            var tokenRecord = tokenRecords.FirstOrDefault();
            if (tokenRecord != null) await _context.Entry(tokenRecord).ReloadAsync();

            if (tokenRecord == null ||
                tokenRecord.UserId != tokenOwner ||
                tokenRecord.IsUsed ||
                tokenRecord.ExpiryDateTime <= DateTime.UtcNow)
            {
                await transaction.RollbackAsync();

                Response.Cookies.Delete(
                    "IPFMMS.PasswordReset",
                    new CookieOptions { Path = "/Account" });

                TempData["ResetNotice"] =
                    "Your password reset session is invalid or expired.";

                return RedirectToAction(nameof(ForgotPassword));
            }

            // Prevent resetting to the existing password.
            var samePasswordResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    model.NewPassword);

            if (samePasswordResult !=
                PasswordVerificationResult.Failed)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    nameof(model.NewPassword),
                    "New password must be different from your current password.");

                return View(model);
            }

            // Update the password using ASP.NET Core's
            // password hashing service.
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                model.NewPassword);

            // Invalidate previously issued authentication cookies.
            user.SecurityVersion++;

            // Consume the reset token.
            tokenRecord.IsUsed = true;

            // Invalidate any other unused password-reset tokens.
            var otherTokens = await _context.PasswordResetTokens
                .Where(t =>
                    t.UserId == user.UserId &&
                    !t.IsUsed &&
                    t.TokenId != tokenRecord.TokenId)
                .ToListAsync();

            foreach (var otherToken in otherTokens)
            {
                otherToken.IsUsed = true;
            }

            // Save password change and token consumption together.
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Remove the temporary reset authorization.
            Response.Cookies.Delete(
                "IPFMMS.PasswordReset",
                new CookieOptions { Path = "/Account" });

            // Unlike normal Change Password, account recovery
            // requires the user to sign in again.
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["SuccessMessage"] =
                "Your password has been reset successfully. " +
                "Please log in with your new password.";

            return RedirectToAction(nameof(Login));
        }

        private Task PopulateResetOtpViewModelAsync(
     ResetPasswordOtpViewModel model)
        {
            model.ResendAvailableAtUtc = null;
            model.ResendCooldownSeconds = 0;

            string? timestampString =
                TempData.Peek("ResetRequestTime")?.ToString();

            if (!long.TryParse(timestampString, out long timestamp))
            {
                return Task.CompletedTask;
            }

            DateTimeOffset requestTime;

            try
            {
                requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Task.CompletedTask;
            }

            DateTimeOffset resendAvailableAt =
                requestTime.AddSeconds(60);

            model.ResendAvailableAtUtc =
                resendAvailableAt.UtcDateTime;

            model.ResendCooldownSeconds = Math.Max(
                0,
                (int)Math.Ceiling(
                    (resendAvailableAt - DateTimeOffset.UtcNow)
                    .TotalSeconds));

            return Task.CompletedTask;
        }

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting("PasswordRecovery")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendResetOtp()
        {
            string email = TempData.Peek("ResetEmail")?
                .ToString()?.Trim().ToLowerInvariant() ?? "";

            const string message =
                "If the account is eligible and the resend limit " +
                "has not been reached, a verification code will be sent.";

            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            TempData["ResetRequestTime"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            DateTime now = DateTime.UtcNow;

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Lock the user row to serialize resend requests.
            var users = await _context.Users
                .FromSqlInterpolated(
                    $@"SELECT * FROM Users
               WHERE Email = {email}
               FOR UPDATE")
                .AsTracking()
                .ToListAsync();

            var user = users.FirstOrDefault();
            if (user != null) await _context.Entry(user).ReloadAsync();

            // Do not reveal account existence or status.
            if (user == null ||
                !AccountStatus.CanLogin(user.AccountStatus))
            {
                await transaction.RollbackAsync();

                TempData["ResetNotice"] = message;

                return RedirectToAction(nameof(VerifyResetOtp));
            }

            // Find all password-reset OTPs issued in the last hour.
            var recentOtps = await _context.EmailOTPs
                .Where(o =>
                    o.UserId == user.UserId &&
                    o.Purpose == EmailOtpPurpose.PasswordReset &&
                    o.CreatedDateTime >= now.AddHours(-1))
                .OrderByDescending(o => o.CreatedDateTime)
                .ToListAsync();

            // Enforce 60-second cooldown.
            if (recentOtps.Count > 0 &&
                recentOtps[0].CreatedDateTime > now.AddSeconds(-60))
            {
                await transaction.RollbackAsync();

                TempData["ResetNotice"] = message;

                return RedirectToAction(nameof(VerifyResetOtp));
            }

            // Maximum six OTP requests per hour.
            if (recentOtps.Count >= 6)
            {
                await transaction.RollbackAsync();

                TempData["ResetNotice"] = message;

                return RedirectToAction(nameof(VerifyResetOtp));
            }

            // Invalidate previous unused password-reset OTPs.
            var oldOtps = await _context.EmailOTPs
                .Where(o =>
                    o.UserId == user.UserId &&
                    o.Purpose == EmailOtpPurpose.PasswordReset &&
                    !o.IsVerified)
                .ToListAsync();

            foreach (var oldOtp in oldOtps)
            {
                oldOtp.ExpiryDateTime = now;
            }

            // Generate a new secure six-digit OTP.
            string otpCode = _otpService.GenerateOtp();

            string otpId = await _idGenerator.GenerateIdAsync("OTP");

            var newOtp = new EmailOTP
            {
                OtpId = otpId,
                UserId = user.UserId,
                OtpCode = _otpService.HashOtp(
                    user.UserId, otpCode),
                Purpose = EmailOtpPurpose.PasswordReset,
                CreatedDateTime = now,
                ExpiryDateTime = now.AddMinutes(5),
                IsVerified = false,
                FailedAttempts = 0
            };

            _context.EmailOTPs.Add(newOtp);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            try
            {
                await _emailService.SendEmailAsync(
                    user.Email,
                    "IPFMMS - New Password Reset Code",
                    $"Hello {user.FullName},\n\n" +
                    $"Your new password reset code is: {otpCode}\n\n" +
                    "This code expires in 5 minutes.\n\n" +
                    "If you did not request this, please ignore this email.\n\n" +
                    "IPFMMS Team");
            }
            catch (Exception)
            {
                // Don't reveal email delivery errors or account details.
            }

            TempData["ResetNotice"] = message;

            return RedirectToAction(nameof(VerifyResetOtp));
        }
    }
}