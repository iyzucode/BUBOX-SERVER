using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Bubox.Api;
using Bubox.Api.BackgroundServices;
using Bubox.Api.Middlewares;
using Bubox.Application;
using Bubox.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// JWT Authentication setup
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "SuperSecretKeyForBuboxPlatformAuthentication2026!MustBe32CharsLong";
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
// Catatan: di belakang reverse proxy, aktifkan ForwardedHeaders agar IP asli yang terbaca.
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
