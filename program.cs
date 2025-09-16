using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using NexusHome.IoT.Data;
using NexusHome.IoT.Services;
using NexusHome.IoT.AI;
using NexusHome.IoT.Energy;
using Microsoft.ML;
using Azure.Identity;
using Microsoft.Azure.Devices.Provisioning.Service;
using Serilog;
using Microsoft.OpenApi.Models;
using NexusHome.IoT.Hubs;
using NexusHome.IoT.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/nexushome-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog();

// --- Service Configuration ---

// 1. Database Context
builder.Services.AddDbContext<NexusHomeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Azure IoT Services
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.UseCredential(new DefaultAzureCredential());
});

builder.Services.AddSingleton(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("IoTHub");
    if (string.IsNullOrEmpty(connectionString))
    {
        Log.Warning("Azure IoT Hub connection string is not configured.");
        return null!;
    }
    return ProvisioningServiceClient.CreateFromConnectionString(connectionString);
});


// 3. JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]!))
        };
    });
builder.Services.AddAuthorization();


// 4. ML.NET Context
builder.Services.AddSingleton<MLContext>();

// 5. Application Services (Dependency Injection)
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IEnergyService, EnergyService>();
builder.Services.AddScoped<IPredictiveMaintenanceService, PredictiveMaintenanceService>();
builder.Services.AddScoped<IAutomationService, AutomationService>();
builder.Services.AddScoped<IMqttService, MqttService>();
builder.Services.AddScoped<IMatterService, MatterService>();
builder.Services.AddScoped<IEnergyOptimizationService, EnergyOptimizationService>();
builder.Services.AddScoped<ISolarPanelService, SolarPanelService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// 6. Background Services
builder.Services.AddHostedService<EnergyMonitoringService>();
builder.Services.AddHostedService<PredictiveMaintenanceBackgroundService>();
builder.Services.AddHostedService<EnergyOptimizationBackgroundService>();
builder.Services.AddHostedService<MqttService>(); // MqttService is now a background service


// 7. SignalR for real-time communication
builder.Services.AddSignalR();


// 8. Controllers and API Endpoints
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NexusHome IoT API",
        Version = "v2.0",
        Description = "A modern, AI-powered smart home energy management system.",
        Contact = new OpenApiContact { Name = "NexusHome Support", Email = "support@nexushome.dev" }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });
});


// 9. CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 10. Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NexusHomeDbContext>();


// --- Application Pipeline Configuration ---
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NexusHome IoT API v2.0");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.UseRouting();

// Map SignalR Hubs
app.MapHub<EnergyMonitoringHub>("/energyHub");
app.MapHub<DeviceStatusHub>("/deviceHub");
app.MapHub<NotificationHub>("/notificationHub");

app.MapControllers();

// Map Health Check endpoint
app.MapHealthChecks("/health");

// --- Database Initialization ---
try
{
    Log.Information("Applying database migrations...");
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();
        dbContext.Database.Migrate();
    }
    Log.Information("Database migrations applied successfully.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "An error occurred while migrating the database.");
    return;
}


Log.Information("NexusHome IoT System is starting...");
app.Run();
