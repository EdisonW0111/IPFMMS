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
            string? userId = context.Principal?
                .FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                await RejectSessionAsync(context);
                return;
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null ||
                !AccountStatus.CanLogin(user.AccountStatus))
            {
                await RejectSessionAsync(context);
                return;
            }
        }

        private static async Task RejectSessionAsync(
            CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}