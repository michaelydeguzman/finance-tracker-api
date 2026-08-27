using FinanceTracker.Domain.Services;
using System.Text;
using System.Threading.RateLimiting;
using FinanceTracker.API.Authentication;
using FinanceTracker.Application.Options;
using FinanceTracker.Application.Services;
using FinanceTracker.Application.Services.Auth;
using FinanceTracker.Application.Services.Email;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using FinanceTracker.Application.Features.Categories.Queries.GetCategories;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FinanceTrackerContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FinanceTrackerDB")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

// --- Auth configuration -----------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));

builder.Services.AddSingleton<IPasswordHashingService, PasswordHashingService>();
builder.Services.AddSingleton<ISecretTokenService, SecretTokenService>();
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Provider is chosen by configuration so the vendor stays a config line, not a rewrite.
// Logging is the default, so an unconfigured environment cannot mail real people.
var emailProvider = builder.Configuration
    .GetSection(EmailOptions.SectionName)
    .GetValue<EmailProvider>(nameof(EmailOptions.Provider));

// Whichever provider is chosen, it is reached through NonFatalEmailSender: these messages
// are sent after the work they describe is already committed, so a dead mail server must
// not turn a completed registration into a failed request.
switch (emailProvider)
{
    case EmailProvider.Smtp:
        builder.Services.AddScoped<SmtpEmailSender>();
        RegisterEmailSender<SmtpEmailSender>();
        break;
    case EmailProvider.Resend:
        builder.Services.AddHttpClient<ResendEmailSender>();
        RegisterEmailSender<ResendEmailSender>();
        break;
    default:
        builder.Services.AddScoped<LoggingEmailSender>();
        RegisterEmailSender<LoggingEmailSender>();
        break;
}

void RegisterEmailSender<TProvider>() where TProvider : class, IEmailSender =>
    builder.Services.AddScoped<IEmailSender>(services => new NonFatalEmailSender(
        services.GetRequiredService<TProvider>(),
        services.GetRequiredService<ILogger<NonFatalEmailSender>>()));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Bound through IOptions rather than read from builder.Configuration inline, so the values
// resolve when the auth handler is first built rather than at startup-script execution.
// Reading them inline captures whatever configuration existed at that instant and misses
// any source layered on afterwards — which is exactly how a test host supplies its own key.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        if (string.IsNullOrWhiteSpace(jwt.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it in user-secrets or the environment.");
        }

        // The handler's default inbound mapping rewrites "sub" to a ClaimTypes URI. Turned
        // off so the claim is read under the same name it was issued with — see
        // JwtAccessTokenIssuer.UserIdClaim.
        bearer.MapInboundClaims = false;

        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

            // No grace period on expiry. The default five minutes would silently extend
            // every access token's life well past the window it was issued for.
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtAccessTokenIssuer.UserIdClaim
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Auth, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
// ----------------------------------------------------------------------------------

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IFrequencyRepository, FrequencyRepository>();
builder.Services.AddScoped<IFrequencyService, FrequencyService>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();

// Add services to the container.

builder.Services.AddControllers();

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new QueryStringApiVersionReader("api-version"),
        new HeaderApiVersionReader("x-api-version")
    );
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(GetCategoriesQuery).Assembly));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

// Authentication must run before authorization — without it the pipeline authorizes an
// anonymous principal and every [Authorize] check has nothing to check.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
