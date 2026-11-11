using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Bubox.Api;
using Bubox.Api.BackgroundServices;
using Bubox.Api.Middlewares;
using Bubox.Application;
using Bubox.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Load YAML Configuration (replaces appsettings.json)
builder.Configuration
    .AddYamlFile("configuration.yml", optional: true, reloadOnChange: true)
    .AddYamlFile($"configuration.{builder.Environment.EnvironmentName}.yml", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add Controllers
builder.Services.AddControllers();

// Swagger / OpenAPI setup
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bubox API",
        Version = "v1",
        Description = "Bubox Backend API with .NET 10, Clean Architecture, and Dapper"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Masukkan JWT Bearer token: {token}"
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc, null),
            new List<string>()
        }
    });
});

// CORS: daftar origin dari konfigurasi (Cors:AllowedOrigins). Produksi web & API satu domain,
// jadi daftar ini hanya untuk origin lain yang memang diizinkan. Kosong + Development = izinkan semua.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin();
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

// Di belakang reverse proxy (Caddy): baca IP & skema asli dari X-Forwarded-*.
// Port API tidak dibuka ke internet (hanya jaringan Docker), jadi header dari proxy dipercaya.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// JWT Authentication setup: key WAJIB dari konfigurasi (env Jwt__Key di produksi), tanpa fallback.
var jwtKey = JwtKeyGuard.Require(builder.Configuration, builder.Environment);
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Bubox.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Bubox.Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorization();

// Rate limiting endpoint auth per alamat IP (melindungi OTP dari tebak paksa & spam email).
// IP asli dari proxy dibaca lewat UseForwardedHeaders di bawah.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            statusCode = StatusCodes.Status429TooManyRequests,
            errorCode = "TOO_MANY_REQUESTS",
            message = "Terlalu banyak permintaan. Coba lagi dalam 1 menit."
        }, cancellationToken);
    };
});

// Register Clean Architecture layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Background job: menutup order yang lewat batas waktu bayar
builder.Services.AddHostedService<OrderExpiryHostedService>();

var app = builder.Build();

// Harus paling awal: IP asli dipakai rate limiter & log.
app.UseForwardedHeaders();

// Global Exception Handler Middleware
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// Enable CORS
app.UseCors();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Bubox API v1");
    });

    // Redirect root '/' to swagger UI
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Map Controllers
app.MapControllers();

// Basic Health Check endpoint
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Bubox.Api",
    timestamp = DateTime.UtcNow
}))
.WithName("HealthCheck")
.WithTags("System");

app.Run();
