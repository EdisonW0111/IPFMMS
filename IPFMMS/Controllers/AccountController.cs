using IPFMMS.Data;
using IPFMMS.Models;
using IPFMMS.Services;
using IPFMMS.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

        public AccountController(
            ApplicationDbContext context,
            IdGeneratorService idGenerator,
            EmailService emailService,
            OtpService otpService,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration,
            TurnstileService turnstileService)
        {
            _context = context;
            _idGenerator = idGenerator;
            _emailService = emailService;
            _otpService = otpService;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _turnstileService = turnstileService;
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
                            o.Purpose == "Registration" &&
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
                            o.Purpose == "Registration" &&
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
                    Purpose = "Registration",
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

            // Lock the OTP record while checking and updating it.
            // This prevents simultaneous attempts from bypassing the limit.
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == model.UserId);

            if (user == null || user.AccountStatus != "Pending")
            {
                await transaction.RollbackAsync();
                return NotFound();
            }

            model.Email = user.Email;

            var otpRecords = await _context.EmailOTPs
                .FromSqlInterpolated(
                    $@"SELECT * FROM EmailOTPs
               WHERE UserId = {model.UserId}
                 AND Purpose = {"Registration"}
                 AND IsVerified = FALSE
               ORDER BY CreatedDateTime DESC, OtpId DESC
               LIMIT 1 FOR UPDATE")
                .AsTracking()
                .ToListAsync();

            var otp = otpRecords.FirstOrDefault();

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

            if (user == null || user.AccountStatus != "Pending")
            {
                await transaction.RollbackAsync();
                return NotFound();
            }

            DateTime now = DateTime.UtcNow;

            var recentOtps = await _context.EmailOTPs
                .Where(o => o.UserId == userId &&
                            o.Purpose == "Registration" &&
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
                            o.Purpose == "Registration" &&
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
                Purpose = "Registration",
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
                    o.Purpose == "Registration")
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
    }
}