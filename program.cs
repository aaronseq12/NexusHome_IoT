using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using NexusHome.Data;
using NexusHome.Services;
using NexusHome.IoT;
using NexusHome.AI;
using NexusHome.Energy;
using Microsoft.ML;
using Azure.Identity;
using Azure.IoT.DeviceProvisioning.Service;
using MQTTnet.AspNetCore;
using Serilog;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/nexushome-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddDbContext<NexusHomeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Azure IoT Hub and Device Provisioning Service
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddServiceBusClient(builder.Configuration.GetConnectionString("ServiceBus"));
    clientBuilder.UseCredential(new DefaultAzureCredential());
});

// Add IoT Hub connection
builder.Services.AddSingleton(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("IoTHub");
    return DeviceProvisioningServiceClient.CreateFromConnectionString(connectionString);
});

// JWT Authentication
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]))
        };
    });

// ML.NET Context
builder.Services.AddSingleton<MLContext>();

// Register application services
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IEnergyService, EnergyService>();
builder.Services.AddScoped<IPredictiveMaintenanceService, PredictiveMaintenanceService>();
builder.Services.AddScoped<IAutomationService, AutomationService>();
builder.Services.AddScoped<IMqttService, MqttService>();
builder.Services.AddScoped<IMatterService, MatterService>();
builder.Services.AddScoped<IEnergyOptimizationService, EnergyOptimizationService>();
builder.Services.AddScoped<ISolarPanelService, SolarPanelService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Background services
builder.Services.AddHostedService<EnergyMonitoringService>();
builder.Services.AddHostedService<PredictiveMaintenanceBackgroundService>();
builder.Services.AddHostedService<EnergyOptimizationBackgroundService>();

// SignalR for real-time updates
builder.Services.AddSignalR();

// MQTT Server
builder.Services.AddHostedMqttServer(mqttServerBuilder =>
{
    mqttServerBuilder.WithDefaultEndpointPort(1883)
                    .WithDefaultEndpointBoundIPAddress(System.Net.IPAddress.Any);
});

// Controllers
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "NexusHome IoT API", 
        Version = "v2.0",
        Description = "Smart Home Energy Management System with AI-powered IoT integration",
        Contact = new OpenApiContact
        {
            Name = "NexusHome Team",
            Email = "support@nexushome.io"
        }
    });
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContext<NexusHomeDbContext>()
    .AddUrlGroup(new Uri(builder.Configuration.GetConnectionString("IoTHub")), "iot-hub");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NexusHome IoT API v2.0");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.UseRouting();

// SignalR Hubs
app.MapHub<EnergyMonitoringHub>("/energyHub");
app.MapHub<DeviceStatusHub>("/deviceHub");
app.MapHub<NotificationHub>("/notificationHub");

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();
    context.Database.EnsureCreated();
}

Log.Information("NexusHome IoT Smart Home Energy Management System starting up...");

app.Run();

// Ensure to flush and stop internal timers/threads before application-exit
Log.CloseAndFlush();