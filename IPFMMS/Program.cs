using Microsoft.EntityFrameworkCore;
using IPFMMS.Data;
using IPFMMS.Services;
using Microsoft.AspNetCore.Identity;
using IPFMMS.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "MySQL connection string is missing.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 46))
    )
);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IdGeneratorService>();
builder.Services.AddScoped<BudgetRolloverService>();
builder.Services.AddScoped<AutomaticSavingsService>();
builder.Services.AddHostedService<AutomaticSavingsWorker>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<PasswordResetTokenService>();
builder.Services.AddHttpClient<TurnstileService>();
builder.Services.AddScoped<CustomCookieAuthenticationEvents>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Authentication page routes
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";

        // Cookie security
        options.Cookie.Name = "IPFMMS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;

        // Default login session
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;

        options.EventsType = typeof(CustomCookieAuthenticationEvents);
    });
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<LoginAttemptService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("PasswordRecovery", context =>
    {
        // Limit requests by client IP address.
        string clientIp =
            context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});
builder.Services.AddScoped<IReceiptStorageService, AzureReceiptStorageService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
