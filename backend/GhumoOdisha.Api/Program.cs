using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using GhumoOdisha.Api.BackgroundServices;
using GhumoOdisha.Api.Filters;
using GhumoOdisha.Api.Logging;
using GhumoOdisha.Api.Middleware;
using GhumoOdisha.Api.Seo;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Auth.Validators;
using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Customers;
using GhumoOdisha.Application.Company;
using GhumoOdisha.Application.Dashboard;
using GhumoOdisha.Application.Destinations;
using GhumoOdisha.Application.Homepage;
using GhumoOdisha.Application.Invoices;
using GhumoOdisha.Application.Logs;
using GhumoOdisha.Application.Notifications;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Refunds;
using GhumoOdisha.Application.SearchLogs;
using GhumoOdisha.Application.Seo;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Infrastructure.Auth;
using GhumoOdisha.Infrastructure.Invoices;
using GhumoOdisha.Infrastructure.Notifications;
using GhumoOdisha.Infrastructure.Payments;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Infrastructure.Persistence.Seed;
using GhumoOdisha.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// QuestPDF requires an explicit license declaration. Community is free for organizations with
// under $1M USD annual gross revenue — confirm that still applies before shipping; otherwise a
// Professional/Enterprise license is needed. See https://www.questpdf.com/license/.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Every line carries the request id + who made the request (see Logging/RequestContextMiddleware).
const string LogLineTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {ErrorRef} {UserRole}{UserId} {SourceContext}: {Message:lj}{NewLine}{Exception}";

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: LogLineTemplate)
        // 180 daily files = CERT-In minimum. In Docker, mount a volume at /app/logs so they survive redeploys.
        .WriteTo.File(context.Configuration["Logging:FilePath"] ?? "logs/ghumo-odisha-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: context.Configuration.GetValue("Logging:RetentionDays", 180),
            outputTemplate: LogLineTemplate);

    // The admin Logs screen reads from the AppLogs table.
    var logConnection = context.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(logConnection))
    {
        configuration.WriteTo.Sink(new DatabaseLogSink(logConnection),
            new Serilog.Configuration.BatchingOptions { BatchSizeLimit = 100, BufferingTimeLimit = TimeSpan.FromSeconds(2), QueueLimit = 10000 });
    }
});

// Add services to the container.

builder.Services.AddControllers(options =>
{
    // Registered first so it also records admin calls the validation filter rejects.
    options.Filters.Add<AdminActivityFilter>();
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
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection(WhatsAppOptions.SectionName));
builder.Services.AddHttpClient<IWhatsAppService, MetaWhatsAppService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection(GoogleAuthOptions.SectionName));
builder.Services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IBookingEmailService, BookingEmailService>();

builder.Services.AddScoped<IPinHasher, PinHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPhoneOtpService, PhoneOtpService>();
builder.Services.AddScoped<ICustomerAuthService, CustomerAuthService>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<ITripService, TripService>();
builder.Services.AddScoped<IDestinationService, DestinationService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IBookingTravellerService, BookingTravellerService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IRefundService, RefundService>();

builder.Services.Configure<RazorpayOptions>(builder.Configuration.GetSection(RazorpayOptions.SectionName));
// Env files saved with Windows line endings (or quoted values) leave a stray \r / quote on the
// key, which Razorpay rejects with 401 even though the key itself is right.
builder.Services.PostConfigure<RazorpayOptions>(o =>
{
    o.KeyId = o.KeyId.Trim().Trim('"', '\'');
    o.KeySecret = o.KeySecret.Trim().Trim('"', '\'');
});
builder.Services.AddHttpClient<IRazorpayService, RazorpayService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<IBookingPaymentService, BookingPaymentService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddHostedService<BookingCompletionBackgroundService>();
builder.Services.AddHostedService<LogRetentionBackgroundService>();
builder.Services.AddScoped<ILogQueryService, LogQueryService>();
builder.Services.AddScoped<ISearchLogService, SearchLogService>();

builder.Services.Configure<CarRentalOptions>(builder.Configuration.GetSection(CarRentalOptions.SectionName));
builder.Services.AddScoped<IDriverAuthService, DriverAuthService>();
builder.Services.AddScoped<IDriverService, DriverService>();
builder.Services.AddScoped<IDriverCarService, DriverCarService>();
builder.Services.AddScoped<IAdminCarService, AdminCarService>();
builder.Services.AddScoped<ICarCatalogService, CarCatalogService>();
// Registered as itself too: the payment service uses its internal confirm/lock helpers.
builder.Services.AddScoped<CarBookingService>();
builder.Services.AddScoped<ICarBookingService>(sp => sp.GetRequiredService<CarBookingService>());
builder.Services.AddScoped<ICarBookingPaymentService, CarBookingPaymentService>();
builder.Services.AddScoped<ICarTripService, CarTripService>();
builder.Services.AddHostedService<CarBookingHoldExpiryBackgroundService>();
builder.Services.Configure<OrganizerContactOptions>(builder.Configuration.GetSection(OrganizerContactOptions.SectionName));
builder.Services.Configure<FeatureOptions>(builder.Configuration.GetSection(FeatureOptions.SectionName));
builder.Services.AddScoped<IOrganizerProfileService, OrganizerProfileService>();
builder.Services.AddScoped<ISiteHeroPhotoService, SiteHeroPhotoService>();
builder.Services.Configure<CompanyOptions>(builder.Configuration.GetSection(CompanyOptions.SectionName));
builder.Services.AddScoped<IInvoiceService, QuestPdfInvoiceService>();
builder.Services.Configure<SeoOptions>(builder.Configuration.GetSection(SeoOptions.SectionName));
builder.Services.AddScoped<ISeoService, SeoService>();
builder.Services.AddScoped<SeoPageRenderer>();

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
// "driver-documents" is private: not in UploadedFilesController's public list, served only via authorized endpoints.
foreach (var category in new[] { "trips", "highlights", "rooms", "vehicles", "organizer", "destinations", "itineraries", "hero", "cars", "drivers", "driver-documents" })
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
    // Invoice-by-email sends to an address the customer types — capped per customer so it can’t be used to spam.
    options.AddPolicy("InvoiceEmail", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.FindFirst("sub")?.Value
                      ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10)
        }));
    // Public partner earnings lookup — generous for a real person retyping a code, tight for guessing.
    options.AddPolicy("PartnerLookup", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1)
        }));
    // Home-page search tracking — plenty for a person changing filters, not enough to flood the table.
    options.AddPolicy("SearchLog", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
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

// Compress text responses (the Angular JS/CSS bundles, HTML and API JSON) — they're 3–5× smaller
// with Brotli/gzip, which is most of the page weight on a phone. Images are already compressed.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["image/svg+xml"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Optimal);

var app = builder.Build();

// The OTP and payment bypasses let anyone sign in as any number and confirm bookings without paying.
// Refuse to start rather than run a non-Development environment with either one switched on.
if (!app.Environment.IsDevelopment() &&
    (builder.Configuration.GetValue<bool>($"{OtpSettings.SectionName}:DevBypassEnabled") ||
     builder.Configuration.GetValue<bool>($"{RazorpayOptions.SectionName}:DevBypassEnabled")))
{
    throw new InvalidOperationException("Otp/Razorpay DevBypassEnabled must be false outside the Development environment.");
}

// Request id first, so every log line of the request (including the summary line below) carries it.
app.UseMiddleware<RequestIdMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0} ms";
    // Only API calls are worth a line; static files and page loads stay out of the logs.
    options.GetLevel = (http, _, ex) =>
        !http.Request.Path.StartsWithSegments("/api") ? Serilog.Events.LogEventLevel.Verbose
        : ex is not null || http.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
        : http.Response.StatusCode >= 400 ? Serilog.Events.LogEventLevel.Warning
        : Serilog.Events.LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnostics, http) =>
    {
        var (userId, role) = UserLogContextMiddleware.CallerOf(http.User);
        if (userId is not null)
        {
            diagnostics.Set(LogProperties.UserId, userId);
            diagnostics.Set(LogProperties.UserRole, role);
        }
    };
});
app.UseResponseCompression();
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

// No UseDefaultFiles: "/" must go through SeoPageRenderer (MapSeo below) rather than being
// served as the raw index.html, so the home page gets its SEO tags too.
app.UseStaticFiles(new StaticFileOptions
{
    // Angular's build files carry a content hash in their name (main-GZHITZBQ.js, styles-CWMVGEIM.css),
    // so a changed file always gets a new name: browsers can keep them for a year without re-checking.
    // index.html itself is served by SeoPageRenderer with no-cache, so a new deploy is picked up at once.
    OnPrepareResponse = ctx =>
    {
        if (HashedBuildFile().IsMatch(ctx.File.Name))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    }
});

app.UseHttpsRedirection();

app.UseAuthentication();
// After authentication so per-customer policies (InvoiceEmail) can read the signed-in user.
app.UseRateLimiter();
app.UseMiddleware<UserLogContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

// Development only: a deliberate crash, to see an unexpected error end to end
// (customer gets an "Error ref", admin finds it on the Logs screen).
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/dev/test-error", () =>
    {
        throw new InvalidOperationException("Test error triggered from /api/dev/test-error (development only).");
    });
}

// Angular SPA fallback + robots.txt + sitemap.xml: any GET without a file extension and not
// under /api (e.g. /trips, /trips/1-puri on a hard refresh) serves index.html — with that page's
// title, description, preview tags and structured data — so the Angular router can take over.
// Extensioned paths (missing static assets) and unmatched /api/* requests still 404.
app.MapSeo();

app.Run();

public partial class Program
{
    /// <summary>Angular build output with a content hash: "chunk-KBHEOLRC.js", "styles-CWMVGEIM.css", "media/x-AB12CD34.woff2".</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"-[A-Z0-9]{8}\.(js|css|woff2?|ttf|svg|png|jpe?g|webp)$")]
    private static partial System.Text.RegularExpressions.Regex HashedBuildFile();
}
