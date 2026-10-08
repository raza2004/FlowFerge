using System.Security.Claims;
using System.Text;
using FlowForge.API.Hubs;
using FlowForge.API.Middleware;
using FlowForge.API.RateLimiting;
using FlowForge.API.Realtime;
using FlowForge.Application;
using FlowForge.Application.Common.Abstractions;
using FlowForge.Infrastructure;
using FlowForge.Infrastructure.Observability;
using FlowForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, logger) => logger.ConfigureFlowForgeLogging(context.Configuration, "FlowForge.API"));
builder.Services.AddFlowForgeTracing(builder.Configuration, "FlowForge.API", tracing =>
    tracing.AddAspNetCoreInstrumentation(options => options.Filter = http =>
        !http.Request.Path.StartsWithSegments("/health") && !http.Request.Path.StartsWithSegments("/hubs")));

// MVC + Swagger
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FlowForge API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {token}"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Our layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddFlowForgeRateLimiting(builder.Configuration);

// CORS for Angular
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("Frontend", p =>
        p.WithOrigins("http://localhost:4200", "http://localhost:4201")
         .AllowAnyHeader().AllowAnyMethod().AllowCredentials());
});

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub"
        };

        // SignalR sends the JWT via ?access_token= on WebSocket/SSE transports
        // because browsers can't set an Authorization header on those connections.
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("SystemAdmin", p => p.RequireClaim("isSystemAdmin", "true"));
});

var app = builder.Build();

// Applying migrations on startup keeps `docker-compose up` a one-command way to run the
// whole stack against a fresh database - no separate `dotnet ef database update` step.
// Migrations are idempotent, so this is a no-op once the schema is already current.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FlowForgeDbContext>();
    db.Database.Migrate();
    FeatureFlagSeeder.EnsureAsync(db).GetAwaiter().GetResult();
}

// Middleware order is important
app.UseMiddleware<ExceptionHandlingMiddleware>();

// One structured line per request (path only, never the query string, which carries the
// SignalR access token). Enriched at the end of the request, once the user is known.
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (http, _, ex) =>
        ex != null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
        : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
        : LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnostics, http) =>
    {
        diagnostics.Set("UserId", http.User.FindFirst("sub")?.Value ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        diagnostics.Set("TenantId", http.User.FindFirst("tenantId")?.Value);
    };
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
// After authentication so the global limit can partition by signed-in user.
if (builder.Configuration.GetValue("RateLimiting:Enabled", true)) app.UseRateLimiter();

// Every log line written while handling a request carries who and which workspace it was for.
app.Use(async (http, next) =>
{
    using (LogContext.PushProperty("UserId", http.User.FindFirst("sub")?.Value ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value))
    using (LogContext.PushProperty("TenantId", http.User.FindFirst("tenantId")?.Value))
        await next();
});
app.MapControllers();
app.MapHub<BoardHub>("/hubs/board");
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapGet("/", () => "FlowForge API is running. See /swagger");

app.Run();

/// <summary>Makes the top-level Program reachable as a generic type argument for WebApplicationFactory&lt;Program&gt; in integration tests.</summary>
public partial class Program { }
