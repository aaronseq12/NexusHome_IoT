using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using NexusHome.IoT.Infrastructure.Data;
using NexusHome.IoT.Core.Services.Interfaces;
using NexusHome.IoT.Infrastructure.Services;
using NexusHome.IoT.Core.Services;
using NexusHome.IoT.Application.Hubs;
using NexusHome.IoT.API.Middleware;
using NexusHome.IoT.Infrastructure.Configuration;
using Serilog;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// Configure Serilog early in the startup process
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/nexushome-.txt", 
        rollingInterval: RollingInterval.Day, 
        retainedFileCountLimit: 30,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1))
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from appsettings
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // Add services to the container
    builder.Services.AddNexusHomeServices(builder.Configuration);
    
    var app = builder.Build();
    
    // Configure the HTTP request pipeline
    app.ConfigureNexusHomePipeline();
    
    // Initialize database and run application
    await app.InitializeDatabaseAsync();
    
    Log.Information("🚀 NexusHome IoT System started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Extension methods for service configuration
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNexusHomeServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Core infrastructure
        services.AddDatabaseServices(configuration);
        services.AddAuthenticationServices(configuration);
        services.AddCachingServices(configuration);
        services.AddMessagingServices(configuration);
        
        // Application services
        services.AddBusinessLogicServices();
        services.AddBackgroundServices();
        
        // API and Web services
        services.AddWebApiServices();
        services.AddRealTimeServices();
        services.AddMonitoringServices();
        
        // External integrations
        services.AddThirdPartyIntegrations(configuration);
        
        return services;
    }
    
    private static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SmartHomeDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Server=localhost;Database=NexusHomeIoT;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true";
            
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
            
            options.EnableSensitiveDataLogging(false);
            options.EnableServiceProviderCaching();
        });
        
        return services;
    }
    
    private static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtAuthentication").Get<JwtAuthenticationSettings>() 
            ?? new JwtAuthenticationSettings();
            
        services.AddSingleton(jwtSettings);
        
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ClockSkew = TimeSpan.FromMinutes(5)
                };
                
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
            
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Administrator"));
            options.AddPolicy("UserAccess", policy => policy.RequireRole("User", "Administrator"));
            options.AddPolicy("DeviceAccess", policy => policy.RequireRole("Device", "User", "Administrator"));
        });
        
        return services;
    }
    
    private static IServiceCollection AddCachingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "NexusHome";
            });
        }
        
        return services;
    }
    
    private static IServiceCollection AddMessagingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MqttBrokerSettings>(configuration.GetSection("MqttBroker"));
        services.AddSingleton<IMqttClientService, MqttClientService>();
        
        return services;
    }
    
    private static IServiceCollection AddBusinessLogicServices(this IServiceCollection services)
    {
        // Core domain services
        services.AddScoped<ISmartDeviceManager, SmartDeviceManager>();
        services.AddScoped<IEnergyConsumptionAnalyzer, EnergyConsumptionAnalyzer>();
        services.AddScoped<IAutomationRuleEngine, AutomationRuleEngine>();
        services.AddScoped<IPredictiveMaintenanceEngine, PredictiveMaintenanceEngine>();
        services.AddScoped<IEnergyOptimizationEngine, EnergyOptimizationEngine>();
        
        // Utility services
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IDataAggregationService, DataAggregationService>();
        services.AddScoped<ISecurityManager, SecurityManager>();
        
        return services;
    }
    
    private static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        services.AddHostedService<DeviceDataCollectionService>();
        services.AddHostedService<EnergyMonitoringBackgroundService>();
        services.AddHostedService<MaintenanceSchedulingService>();
        services.AddHostedService<AutomationRuleProcessorService>();
        
        return services;
    }
    
    private static IServiceCollection AddWebApiServices(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            options.ReturnHttpNotAcceptable = true;
            options.RespectBrowserAcceptHeader = true;
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.WriteIndented = false;
        });
        
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo 
            { 
                Title = "NexusHome Smart IoT API", 
                Version = "v2.0",
                Description = "Advanced Smart Home Energy Management & IoT Control System",
                Contact = new OpenApiContact 
                { 
                    Name = "NexusHome Development Team", 
                    Email = "developers@nexushome.tech",
                    Url = new Uri("https://github.com/aaronseq12/NexusHome_IoT")
                }
            });
            
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
        
        // Rate limiting
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("ApiLimiter", rateLimiterOptions =>
            {
                rateLimiterOptions.PermitLimit = 1000;
                rateLimiterOptions.Window = TimeSpan.FromMinutes(1);
                rateLimiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                rateLimiterOptions.QueueLimit = 100;
            });
        });
        
        // CORS
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });
        
        return services;
    }
    
    private static IServiceCollection AddRealTimeServices(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });
        
        return services;
    }
    
    private static IServiceCollection AddMonitoringServices(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<SmartHomeDbContext>("database")
            .AddCheck("mqtt_connection", () => HealthCheckResult.Healthy("MQTT broker connection is healthy"))
            .AddCheck("system_memory", () => 
            {
                var allocatedMemory = GC.GetTotalMemory(false);
                var memoryThreshold = 1024L * 1024L * 1024L; // 1GB
                
                return allocatedMemory < memoryThreshold 
                    ? HealthCheckResult.Healthy($"Memory usage: {allocatedMemory / (1024 * 1024)} MB")
                    : HealthCheckResult.Degraded($"High memory usage: {allocatedMemory / (1024 * 1024)} MB");
            });
            
        return services;
    }
    
    private static IServiceCollection AddThirdPartyIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        // Weather API integration
        var weatherApiKey = configuration["WeatherApi:ApiKey"];
        if (!string.IsNullOrEmpty(weatherApiKey))
        {
            services.Configure<WeatherApiSettings>(configuration.GetSection("WeatherApi"));
            services.AddHttpClient<IWeatherDataProvider, OpenWeatherMapProvider>();
        }
        
        // Utility provider integration
        services.AddHttpClient<IUtilityPriceProvider, UtilityPriceProvider>();
        
        return services;
    }
}

/// <summary>
/// Extension methods for application pipeline configuration
/// </summary>
public static class WebApplicationExtensions
{
    public static WebApplication ConfigureNexusHomePipeline(this WebApplication app)
    {
        // Development-specific middleware
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => 
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "NexusHome IoT API v2.0");
                c.RoutePrefix = "api-docs";
                c.DisplayRequestDuration();
                c.EnableDeepLinking();
            });
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }
        
        // Core middleware pipeline
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        
        app.UseRouting();
        app.UseCors();
        app.UseRateLimiter();
        
        // Custom middleware
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        
        app.UseAuthentication();
        app.UseAuthorization();
        
        // SignalR Hubs
        app.MapHub<SmartDeviceStatusHub>("/hubs/device-status");
        app.MapHub<EnergyMonitoringHub>("/hubs/energy-monitoring");
        app.MapHub<SystemNotificationHub>("/hubs/notifications");
        
        // API Controllers
        app.MapControllers().RequireRateLimiting("ApiLimiter");
        
        // Health checks
        app.MapHealthChecks("/health/ready");
        app.MapHealthChecks("/health/live");
        
        // Minimal API endpoints for high-performance scenarios
        app.MapPost("/api/v2/devices/telemetry", HandleDeviceTelemetry)
            .RequireAuthorization("DeviceAccess")
            .WithTags("Device Data")
            .WithOpenApi();
            
        return app;
    }
    
    private static async Task<IResult> HandleDeviceTelemetry(
        DeviceTelemetryRequest request,
        ISmartDeviceManager deviceManager,
        ILogger<Program> logger)
    {
        try
        {
            await deviceManager.ProcessTelemetryDataAsync(request);
            return Results.Accepted();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process telemetry data for device {DeviceId}", request.DeviceId);
            return Results.Problem("Failed to process telemetry data");
        }
    }
    
    public static async Task<WebApplication> InitializeDatabaseAsync(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SmartHomeDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation("🔄 Initializing database...");
            
            await context.Database.MigrateAsync();
            await DatabaseSeeder.SeedAsync(context, scope.ServiceProvider, logger);
            
            logger.LogInformation("✅ Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "❌ Database initialization failed");
            throw;
        }
        
        return app;
    }
}

/// <summary>
/// Request model for device telemetry
/// </summary>
public record DeviceTelemetryRequest(
    string DeviceId,
    Dictionary<string, object> SensorData,
    DateTime Timestamp);
