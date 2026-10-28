using System.Security.Claims;
using IPFMMS.Data;
using IPFMMS.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace IPFMMS.Services
{
    public class CustomCookieAuthenticationEvents
        : CookieAuthenticationEvents
    {
        private readonly ApplicationDbContext _context;

        public CustomCookieAuthenticationEvents(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public override async Task ValidatePrincipal(
            CookieValidatePrincipalContext context)
        {
            var userId = context.Principal?
                .FindFirstValue(ClaimTypes.NameIdentifier);

            var cookieVersion = context.Principal?
                .FindFirst("SecurityVersion")?.Value;

            // Reject cookies without valid identity/version data.
            if (string.IsNullOrWhiteSpace(userId) ||
                !int.TryParse(cookieVersion, out int version))
            {
                await RejectSession(context);
                return;
            }

            // Retrieve current account information from MySQL.
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            // Reject deleted, disabled or missing accounts.
            if (user == null ||
                !AccountStatus.CanLogin(user.AccountStatus))
            {
                await RejectSession(context);
                return;
            }

            // Reject sessions created before security version changed.
            if (version != user.SecurityVersion)
            {
                await RejectSession(context);
                return;
            }

            // Ensure cookie role still matches the database role.
            var cookieRole = context.Principal?
                .FindFirstValue(ClaimTypes.Role);

            if (cookieRole != user.Role)
            {
                await RejectSession(context);
                return;
            }
        }

        private static async Task RejectSession(
            CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
