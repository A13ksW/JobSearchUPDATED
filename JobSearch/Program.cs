using JobSearch.Components;
using JobSearch.Data;
using JobSearch.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// DODAJ TĘ LINIĘ: Wymagane dla śladu audytowego w serwisie
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
    .AddIdentityCookies();

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"))
    .AddPolicy("RequireModerator", policy => policy.RequireRole("Moderator"));


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
// 1) Najpierw fabryka (singleton)
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// 2) Potem zwykły DbContext (scoped) używany np. przez Identity/Pages
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);
    // klucz: użyj service providera, żeby uniknąć konfliktu opcji
    options.UseApplicationServiceProvider(sp);
});

builder.Services.AddScoped<IJobOfferService, JobOfferService>();
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddHostedService<ExpiredOfferService>();
builder.Services.AddScoped<INipValidationService, NipValidationService>();
builder.Services.AddHttpClient<INipValidationService, NipValidationService>(client =>
{
    client.BaseAddress = new Uri("https://wl-api.mf.gov.pl/");
});
builder.Services.AddQuickGridEntityFrameworkAdapter();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.SignIn.RequireConfirmedAccount = false; // for demo purposes
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' wss:; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'";
    await next();
});

await IdentitySeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseMigrationsEndPoint();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();