using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using GhumoOdisha.Api.BackgroundServices;
using GhumoOdisha.Api.Filters;
using GhumoOdisha.Api.Middleware;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Auth.Validators;
using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Customers;
using GhumoOdisha.Application.Company;
using GhumoOdisha.Application.Dashboard;
using GhumoOdisha.Application.Destinations;
using GhumoOdisha.Application.Homepage;
using GhumoOdisha.Application.Invoices;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Infrastructure.Auth;
using GhumoOdisha.Infrastructure.Invoices;
using GhumoOdisha.Infrastructure.Payments;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Infrastructure.Persistence.Seed;
using GhumoOdisha.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// QuestPDF requires an explicit license declaration. Community is free for organizations with
// under $1M USD annual gross revenue — confirm that still applies before shipping; otherwise a
// Professional/Enterprise license is needed. See https://www.questpdf.com/license/.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/ghumo-odisha-.log", rollingInterval: RollingInterval.Day));

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<GhumoOdishaDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

builder.Services.AddScoped<IGhumoOdishaDbContext>(sp => sp.GetRequiredService<GhumoOdishaDbContext>());

builder.Services.AddMemoryCache();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.Configure<OtpSettings>(builder.Configuration.GetSection(OtpSettings.SectionName));
builder.Services.Configure<Fast2SmsOptions>(builder.Configuration.GetSection(Fast2SmsOptions.SectionName));
builder.Services.AddHttpClient<IFast2SmsWhatsAppService, Fast2SmsWhatsAppService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<IPinHasher, PinHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ICustomerAuthService, CustomerAuthService>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddScoped<IDestinationService, DestinationService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.Configure<RazorpayOptions>(builder.Configuration.GetSection(RazorpayOptions.SectionName));
builder.Services.AddHttpClient<IRazorpayService, RazorpayService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<IBookingPaymentService, BookingPaymentService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddHostedService<BookingCompletionBackgroundService>();

builder.Services.Configure<OrganizerContactOptions>(builder.Configuration.GetSection(OrganizerContactOptions.SectionName));
builder.Services.AddScoped<IOrganizerProfileService, OrganizerProfileService>();
builder.Services.AddScoped<ISiteHeroPhotoService, SiteHeroPhotoService>();
builder.Services.Configure<CompanyOptions>(builder.Configuration.GetSection(CompanyOptions.SectionName));
builder.Services.AddScoped<IInvoiceService, QuestPdfInvoiceService>();

// Persistent storage root (uploaded photos, and anywhere else the app writes at runtime) lives
// outside the deployed application directory — see Storage:RootPath / Storage__RootPath — so a
// redeploy of the app can never wipe it. Fails fast if it's not configured rather than silently
// falling back to somewhere inside the app folder.
var storageRootPath = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()?.RootPath;
if (string.IsNullOrWhiteSpace(storageRootPath))
{
    throw new InvalidOperationException(
        "Storage:RootPath is not configured. Set it in appsettings.Development.json for local development, " +
        "or via the Storage__RootPath environment variable in production.");
}

var uploadsBasePath = Path.Combine(storageRootPath, "Uploads");
foreach (var category in new[] { "trips", "highlights", "rooms", "vehicles", "organizer", "destinations", "itineraries", "hero" })
{
    Directory.CreateDirectory(Path.Combine(uploadsBasePath, category));
}

// Reserved for future use (e.g. persisted booking documents) — created now so the full
// persistent tree exists from day one and callers never need to worry about a missing folder.
Directory.CreateDirectory(Path.Combine(storageRootPath, "Documents"));
Directory.CreateDirectory(Path.Combine(storageRootPath, "Invoices"));
Directory.CreateDirectory(Path.Combine(storageRootPath, "Temp"));

builder.Services.Configure<ImageStorageOptions>(options =>
{
    options.BasePath = uploadsBasePath;
    options.PublicUrlPrefix = "/uploads";
});
builder.Services.AddScoped<IImageStorage, LocalImageStorage>();

builder.Services.AddValidatorsFromAssemblyContaining<RequestOtpRequestValidator>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthIp", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1)
        }));
});

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("Default");
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Schema migration + first-admin seed, in every environment.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GhumoOdishaDbContext>();
    var pinHasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (app.Configuration.GetValue("Database:AutoMigrate", true))
    {
        // Deliberately not caught: serving bookings against a missing or half-migrated schema is
        // worse than failing to start, so a migration error stops the app here.
        logger.LogInformation("Applying pending database migrations.");
        await db.Database.MigrateAsync();
    }

    // Admin credentials come from config (user-secrets / environment variables), never from code.
    // Missing credentials just skip admin creation; they never fall back to a default password.
    var adminUsername = app.Configuration["Seed:AdminUsername"];
    var adminPassword = app.Configuration["Seed:AdminPassword"];
    AdminSeed? adminSeed = null;
    if (!string.IsNullOrWhiteSpace(adminUsername) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        adminSeed = new AdminSeed(adminUsername, adminPassword, app.Configuration["Seed:AdminEmail"] ?? "admin@ghumoodisha.in");
    }
    else
    {
        logger.LogWarning("Seed:AdminUsername / Seed:AdminPassword not configured; skipping admin account seed.");
    }

    await DbSeeder.SeedAsync(
        db,
        pinHasher,
        adminSeed,
        overwriteExistingAdmin: app.Environment.IsDevelopment(),
        seedDemoContent: app.Configuration.GetValue<bool>("Seed:DemoContent"));
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Angular SPA fallback: any GET without a file extension and not under /api
// (e.g. /trips, /booking, /trip/123 on a hard refresh) serves index.html so
// the Angular router can take over client-side. Extensioned paths (missing
// static assets) and unmatched /api/* requests still fall through to a 404.
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api).*$)}", "index.html");

app.Run();
