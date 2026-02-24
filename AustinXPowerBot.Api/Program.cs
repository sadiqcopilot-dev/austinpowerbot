using System;
using System.Text;
using AustinXPowerBot.Api.Data;
using AustinXPowerBot.Api.Entities;
using AustinXPowerBot.Api.Hubs;
using AustinXPowerBot.Api.Services;
using AustinXPowerBot.Shared.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Allow running without a database for local testing. If a connection string
// named "Default" is present, register the DbContext and run migrations.
var defaultConn = builder.Configuration.GetConnectionString("Default");
// Enable database-backed features when a connection string is provided.
var useDatabase = !string.IsNullOrWhiteSpace(defaultConn);

if (useDatabase)
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(defaultConn));

    builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
    builder.Services.AddScoped<IRealtimeDispatcher, RealtimeDispatcher>();

    // Identity
    builder.Services.AddIdentity<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<long>>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
}

// JWT settings: only required when using authentication. Provide defaults
// when running without DB to keep the app usable for local testing.
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? (useDatabase ? throw new InvalidOperationException("Jwt:Issuer is required.") : "local-issuer");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? (useDatabase ? throw new InvalidOperationException("Jwt:Audience is required.") : "local-audience");
var jwtKey = builder.Configuration["Jwt:Key"] ?? (useDatabase ? throw new InvalidOperationException("Jwt:Key is required.") : "dev-secret-key-please-change");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrWhiteSpace(accessToken)
                    && path.StartsWithSegments(SignalREvents.RealtimeHubPath))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "AustinXPowerBot.Api", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token as: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

if (useDatabase)
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<RealtimeHub>(SignalREvents.RealtimeHubPath);

app.MapGet("/", () => Results.Ok(new { name = "AustinXPowerBot.Api", status = "running" }));

app.Run();
