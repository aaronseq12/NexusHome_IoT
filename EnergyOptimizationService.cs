using Microsoft.EntityFrameworkCore;
using NexusHome.Models;
using NexusHome.Data;
using NexusHome.IoT;
using System.Text.Json;

namespace NexusHome.Energy
{
    public interface IEnergyOptimizationService
    {
        Task<OptimizationResult> OptimizeEnergyUsageAsync(DateTime startTime, DateTime endTime);
        Task<LoadShiftingRecommendation> GenerateLoadShiftingRecommendationsAsync();
        Task<BatteryOptimizationPlan> OptimizeBatteryUsageAsync();
        Task<SolarOptimizationPlan> OptimizeSolarEnergyUsageAsync();
        Task<CostOptimizationResult> OptimizeEnergyCostsAsync();
        Task<DemandResponseResult> HandleDemandResponseEventAsync(DemandResponseEvent demandEvent);
        Task<EnergyForecast> ForecastEnergyDemandAsync(DateTime startDate, int forecastDays);
        Task ExecuteOptimizationPlanAsync(OptimizationPlan plan);
        Task<ComfortOptimizationResult> OptimizeComfortVsEfficiencyAsync(ComfortPreferences preferences);
    }

    public class EnergyOptimizationService : IEnergyOptimizationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EnergyOptimizationService> _logger;
        private readonly IMqttService _mqttService;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, decimal> _energyRates;
        private readonly TimeSpan _peakHoursStart;
        private readonly TimeSpan _peakHoursEnd;
        private readonly TimeSpan _offPeakStart;
        private readonly TimeSpan _offPeakEnd;

        public EnergyOptimizationService(
            IServiceProvider serviceProvider,
            ILogger<EnergyOptimizationService> logger,
            IMqttService mqttService,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _mqttService = mqttService;
            _configuration = configuration;

            // Load energy rates and time periods from configuration
            _energyRates = new Dictionary<string, decimal>
            {
                ["peak"] = _configuration.GetValue<decimal>("EnergyManagement:EnergyRates:PeakRate"),
                ["standard"] = _configuration.GetValue<decimal>("EnergyManagement:EnergyRates:StandardRate"),
                ["offpeak"] = _configuration.GetValue<decimal>("EnergyManagement:EnergyRates:OffPeakRate"),
                ["solar"] = _configuration.GetValue<decimal>("EnergyManagement:EnergyRates:SolarFeedInRate")
            };

            TimeSpan.TryParse(_configuration["EnergyManagement:PeakHours:Start"], out _peakHoursStart);
            TimeSpan.TryParse(_configuration["EnergyManagement:PeakHours:End"], out _peakHoursEnd);
            TimeSpan.TryParse(_configuration["EnergyManagement:OffPeakHours:Start"], out _offPeakStart);
            TimeSpan.TryParse(_configuration["EnergyManagement:OffPeakHours:End"], out _offPeakEnd);
        }

        public async Task<OptimizationResult> OptimizeEnergyUsageAsync(DateTime startTime, DateTime endTime)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                _logger.LogInformation("Starting energy optimization for period {StartTime} to {EndTime}", startTime, endTime);

                // Get current energy consumption data
                var currentConsumption = await GetCurrentEnergyConsumptionAsync(context);
                
                // Get weather forecast for solar prediction
                var weatherForecast = await GetWeatherForecastAsync(context, startTime, endTime);
                
                // Get battery status
                var batteryStatus = await GetCurrentBatteryStatusAsync(context);
                
                // Get solar generation forecast
                var solarForecast = await ForecastSolarGenerationAsync(context, weatherForecast);

                // Generate optimization strategies
                var strategies = new List<OptimizationStrategy>
                {
                    await GenerateLoadShiftingStrategyAsync(context, startTime, endTime),
                    await GeneratePeakShavingStrategyAsync(context, currentConsumption),
                    await GenerateBatteryOptimizationStrategyAsync(context, batteryStatus, solarForecast),
                    await GenerateThermalOptimizationStrategyAsync(context, weatherForecast),
                    await GenerateApplianceSchedulingStrategyAsync(context, startTime, endTime)
                };

                // Calculate potential savings for each strategy
                var optimizedStrategies = new List<OptimizationStrategy>();
                foreach (var strategy in strategies.Where(s => s != null))
                {
                    strategy.PotentialSavings = await CalculatePotentialSavingsAsync(context, strategy);
                    strategy.ComfortImpact = CalculateComfortImpact(strategy);
                    strategy.ImplementationComplexity = CalculateImplementationComplexity(strategy);
                    
                    if (strategy.PotentialSavings > 0)
                    {
                        optimizedStrategies.Add(strategy);
                    }
                }

                // Sort strategies by savings potential and comfort impact
                var rankedStrategies = optimizedStrategies
                    .OrderByDescending(s => s.PotentialSavings / (1 + s.ComfortImpact))
                    .ToList();

                // Create comprehensive optimization result
                var result = new OptimizationResult
                {
                    OptimizationTimestamp = DateTime.UtcNow,
                    OptimizationPeriod = new DateRange { Start = startTime, End = endTime },
                    CurrentConsumption = currentConsumption,
                    Strategies = rankedStrategies,
                    TotalPotentialSavings = rankedStrategies.Sum(s => s.PotentialSavings),
                    EstimatedCostSavings = rankedStrategies.Sum(s => s.PotentialSavings * GetCurrentEnergyRate()),
                    ComfortScore = CalculateOverallComfortScore(rankedStrategies),
                    EnvironmentalImpact = CalculateEnvironmentalImpact(rankedStrategies),
                    RecommendedActions = GenerateActionRecommendations(rankedStrategies),
                    ImplementationPriority = rankedStrategies.Take(3).ToList()
                };

                _logger.LogInformation("Energy optimization completed. Potential savings: {Savings:C}", result.EstimatedCostSavings);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during energy optimization");
                throw;
            }
        }

        public async Task<LoadShiftingRecommendation> GenerateLoadShiftingRecommendationsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                // Get devices that can be shifted (deferrable loads)
                var deferrableDevices = await context.Devices
                    .Where(d => d.Type == DeviceType.WashingMachine ||
                               d.Type == DeviceType.Dryer ||
                               d.Type == DeviceType.Dishwasher ||
                               d.Type == DeviceType.ElectricVehicleCharger)
                    .Where(d => d.Status == DeviceStatus.Active)
                    .ToListAsync();

                var recommendations = new List<LoadShiftingAction>();

                foreach (var device in deferrableDevices)
                {
                    var currentSchedule = await GetDeviceCurrentScheduleAsync(context, device.Id);
                    var optimalSchedule = CalculateOptimalSchedule(device, currentSchedule);

                    if (optimalSchedule.Any())
                    {
                        var potentialSavings = await CalculateLoadShiftingSavingsAsync(context, device, currentSchedule, optimalSchedule);

                        recommendations.Add(new LoadShiftingAction
                        {
                            DeviceId = device.Id,
                            DeviceName = device.Name,
                            CurrentSchedule = currentSchedule,
                            RecommendedSchedule = optimalSchedule,
                            PotentialSavings = potentialSavings,
                            ShiftReason = DetermineShiftReason(currentSchedule, optimalSchedule),
                            Priority = potentialSavings > 5 ? LoadShiftingPriority.High : LoadShiftingPriority.Medium
                        });
                    }
                }

                return new LoadShiftingRecommendation
                {
                    GeneratedAt = DateTime.UtcNow,
                    Actions = recommendations.OrderByDescending(r => r.PotentialSavings).ToList(),
                    TotalPotentialSavings = recommendations.Sum(r => r.PotentialSavings),
                    OptimizationHorizon = TimeSpan.FromHours(24),
                    Confidence = CalculateLoadShiftingConfidence(recommendations)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating load shifting recommendations");
                throw;
            }
        }

        public async Task<BatteryOptimizationPlan> OptimizeBatteryUsageAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                var batteryDevices = await context.Devices
                    .Where(d => d.Type == DeviceType.BatteryStorage && d.Status == DeviceStatus.Active)
                    .ToListAsync();

                var optimizationPlans = new List<BatteryOptimizationAction>();

                foreach (var battery in batteryDevices)
                {
                    var currentStatus = await GetLatestBatteryStatusAsync(context, battery.Id);
                    var solarForecast = await GetSolarGenerationForecastAsync(context, DateTime.UtcNow, DateTime.UtcNow.AddHours(24));
                    var demandForecast = await GetEnergyDemandForecastAsync(context, DateTime.UtcNow, DateTime.UtcNow.AddHours(24));

                    var chargingPlan = OptimizeBatteryCharging(currentStatus, solarForecast, demandForecast);
                    var dischargingPlan = OptimizeBatteryDischarging(currentStatus, demandForecast);

                    optimizationPlans.Add(new BatteryOptimizationAction
                    {
                        BatteryId = battery.Id,
                        BatteryName = battery.Name,
                        CurrentChargeLevel = currentStatus?.ChargeLevel ?? 0,
                        ChargingSchedule = chargingPlan,
                        DischargingSchedule = dischargingPlan,
                        ExpectedSavings = CalculateBatterySavings(chargingPlan, dischargingPlan),
                        OptimizationReason = "Cost optimization and peak shaving"
                    });
                }

                return new BatteryOptimizationPlan
                {
                    GeneratedAt = DateTime.UtcNow,
                    Actions = optimizationPlans,
                    TotalExpectedSavings = optimizationPlans.Sum(a => a.ExpectedSavings),
                    OptimizationHorizon = TimeSpan.FromHours(24),
                    Confidence = 0.85 // Based on historical performance
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing battery usage");
                throw;
            }
        }

        public async Task<SolarOptimizationPlan> OptimizeSolarEnergyUsageAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                var solarDevices = await context.Devices
                    .Where(d => d.Type == DeviceType.SolarInverter && d.Status == DeviceStatus.Active)
                    .ToListAsync();

                var optimizationActions = new List<SolarOptimizationAction>();

                foreach (var solarDevice in solarDevices)
                {
                    var generationForecast = await GetSolarGenerationForecastAsync(context, DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
                    var demandForecast = await GetEnergyDemandForecastAsync(context, DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

                    // Optimize direct consumption vs. battery storage vs. grid export
                    var directConsumptionPlan = OptimizeDirectSolarConsumption(generationForecast, demandForecast);
                    var storageAllocationPlan = OptimizeSolarToStorage(generationForecast, demandForecast);
                    var gridExportPlan = OptimizeSolarToGrid(generationForecast, demandForecast);

                    optimizationActions.Add(new SolarOptimizationAction
                    {
                        SolarDeviceId = solarDevice.Id,
                        SolarDeviceName = solarDevice.Name,
                        ExpectedGeneration = generationForecast.Sum(f => f.PredictedGeneration),
                        DirectConsumptionPlan = directConsumptionPlan,
                        StorageAllocationPlan = storageAllocationPlan,
                        GridExportPlan = gridExportPlan,
                        OptimizationValue = CalculateSolarOptimizationValue(directConsumptionPlan, storageAllocationPlan, gridExportPlan)
                    });
                }

                return new SolarOptimizationPlan
                {
                    GeneratedAt = DateTime.UtcNow,
                    Actions = optimizationActions,
                    TotalOptimizationValue = optimizationActions.Sum(a => a.OptimizationValue),
                    SelfConsumptionRatio = CalculateSelfConsumptionRatio(optimizationActions),
                    OptimizationHorizon = TimeSpan.FromDays(1)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing solar energy usage");
                throw;
            }
        }

        public async Task<CostOptimizationResult> OptimizeEnergyCostsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                var devices = await context.Devices
                    .Where(d => d.Status == DeviceStatus.Active)
                    .ToListAsync();

                var costOptimizations = new List<CostOptimizationAction>();

                // Analyze each device's cost impact
                foreach (var device in devices)
                {
                    var consumptionHistory = await context.EnergyConsumptions
                        .Where(e => e.DeviceId == device.Id)
                        .Where(e => e.Timestamp >= DateTime.UtcNow.AddDays(-7))
                        .OrderByDescending(e => e.Timestamp)
                        .ToListAsync();

                    if (consumptionHistory.Any())
                    {
                        var currentCost = consumptionHistory.Average(c => c.Cost);
                        var optimizedCost = CalculateOptimizedCost(device, consumptionHistory);

                        if (optimizedCost < currentCost)
                        {
                            costOptimizations.Add(new CostOptimizationAction
                            {
                                DeviceId = device.Id,
                                DeviceName = device.Name,
                                CurrentDailyCost = currentCost,
                                OptimizedDailyCost = optimizedCost,
                                PotentialDailySavings = currentCost - optimizedCost,
                                OptimizationMethods = GenerateCostOptimizationMethods(device, consumptionHistory),
                                ImplementationDifficulty = AssessCostOptimizationDifficulty(device)
                            });
                        }
                    }
                }

                // Add system-wide cost optimizations
                var systemOptimizations = await GenerateSystemWideCostOptimizationsAsync(context);
                costOptimizations.AddRange(systemOptimizations);

                return new CostOptimizationResult
                {
                    GeneratedAt = DateTime.UtcNow,
                    Actions = costOptimizations.OrderByDescending(a => a.PotentialDailySavings).ToList(),
                    TotalDailySavings = costOptimizations.Sum(a => a.PotentialDailySavings),
                    TotalMonthlySavings = costOptimizations.Sum(a => a.PotentialDailySavings) * 30,
                    TotalYearlySavings = costOptimizations.Sum(a => a.PotentialDailySavings) * 365,
                    PaybackPeriod = CalculatePaybackPeriod(costOptimizations),
                    Confidence = 0.8
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing energy costs");
                throw;
            }
        }

        public async Task<DemandResponseResult> HandleDemandResponseEventAsync(DemandResponseEvent demandEvent)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                _logger.LogInformation("Handling demand response event: {EventType} from {StartTime} to {EndTime}",
                    demandEvent.EventType, demandEvent.StartTime, demandEvent.EndTime);

                var responseActions = new List<DemandResponseAction>();
                var controllableDevices = await GetControllableDevicesAsync(context);

                switch (demandEvent.EventType)
                {
                    case DemandResponseEventType.PeakShaving:
                        responseActions.AddRange(await GeneratePeakShavingActionsAsync(controllableDevices, demandEvent));
                        break;

                    case DemandResponseEventType.LoadReduction:
                        responseActions.AddRange(await GenerateLoadReductionActionsAsync(controllableDevices, demandEvent));
                        break;

                    case DemandResponseEventType.FrequencyRegulation:
                        responseActions.AddRange(await GenerateFrequencyRegulationActionsAsync(controllableDevices, demandEvent));
                        break;

                    case DemandResponseEventType.EmergencyResponse:
                        responseActions.AddRange(await GenerateEmergencyResponseActionsAsync(controllableDevices, demandEvent));
                        break;
                }

                // Execute high-priority actions immediately
                var immediateActions = responseActions.Where(a => a.Priority == DemandResponsePriority.Immediate).ToList();
                foreach (var action in immediateActions)
                {
                    await ExecuteDemandResponseActionAsync(action);
                }

                // Schedule medium and low priority actions
                var scheduledActions = responseActions.Where(a => a.Priority != DemandResponsePriority.Immediate).ToList();
                await ScheduleDemandResponseActionsAsync(scheduledActions, demandEvent);

                var totalReduction = responseActions.Sum(a => a.PowerReduction);
                var incentiveEarnings = CalculateIncentiveEarnings(demandEvent, totalReduction);

                return new DemandResponseResult
                {
                    EventId = demandEvent.EventId,
                    ResponseTimestamp = DateTime.UtcNow,
                    Actions = responseActions,
                    TotalPowerReduction = totalReduction,
                    TargetReduction = demandEvent.TargetReduction,
                    ReductionAchieved = totalReduction >= demandEvent.TargetReduction,
                    IncentiveEarnings = incentiveEarnings,
                    ComfortImpact = CalculateDemandResponseComfortImpact(responseActions),
                    Duration = demandEvent.EndTime - demandEvent.StartTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling demand response event {EventId}", demandEvent.EventId);
                throw;
            }
        }

        public async Task<EnergyForecast> ForecastEnergyDemandAsync(DateTime startDate, int forecastDays)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                // Get historical consumption data
                var historicalData = await context.EnergyConsumptions
                    .Where(e => e.Timestamp >= DateTime.UtcNow.AddDays(-90))
                    .GroupBy(e => new { e.Timestamp.Date, e.Timestamp.Hour })
                    .Select(g => new
                    {
                        Date = g.Key.Date,
                        Hour = g.Key.Hour,
                        AverageConsumption = g.Average(e => e.PowerConsumption)
                    })
                    .OrderBy(x => x.Date)
                    .ThenBy(x => x.Hour)
                    .ToListAsync();

                // Get weather forecast for the prediction period
                var weatherForecast = await GetWeatherForecastAsync(context, startDate, startDate.AddDays(forecastDays));

                var forecastPoints = new List<EnergyForecastPoint>();

                for (int day = 0; day < forecastDays; day++)
                {
                    var forecastDate = startDate.AddDays(day);
                    var dayOfWeek = forecastDate.DayOfWeek;
                    var isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;

                    for (int hour = 0; hour < 24; hour++)
                    {
                        var timestamp = forecastDate.AddHours(hour);
                        var historicalAverage = GetHistoricalAverage(historicalData, dayOfWeek, hour);
                        var weatherAdjustment = CalculateWeatherAdjustment(weatherForecast, timestamp);
                        var seasonalAdjustment = CalculateSeasonalAdjustment(timestamp);

                        var baselineForecast = historicalAverage * weatherAdjustment * seasonalAdjustment;

                        forecastPoints.Add(new EnergyForecastPoint
                        {
                            Timestamp = timestamp,
                            PredictedConsumption = Math.Round(baselineForecast, 4),
                            Confidence = CalculateForecastConfidence(historicalData, dayOfWeek, hour),
                            PredictionInterval = new Range<decimal>(
                                Math.Round(baselineForecast * 0.8m, 4),
                                Math.Round(baselineForecast * 1.2m, 4)
                            ),
                            WeatherImpact = weatherAdjustment,
                            SeasonalFactor = seasonalAdjustment
                        });
                    }
                }

                return new EnergyForecast
                {
                    GeneratedAt = DateTime.UtcNow,
                    ForecastPeriod = new DateRange { Start = startDate, End = startDate.AddDays(forecastDays) },
                    ForecastPoints = forecastPoints,
                    TotalPredictedConsumption = forecastPoints.Sum(p => p.PredictedConsumption),
                    AverageConfidence = forecastPoints.Average(p => p.Confidence),
                    PeakDemandPeriods = IdentifyPeakDemandPeriods(forecastPoints),
                    LowDemandPeriods = IdentifyLowDemandPeriods(forecastPoints)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forecasting energy demand");
                throw;
            }
        }

        public async Task ExecuteOptimizationPlanAsync(OptimizationPlan plan)
        {
            try
            {
                _logger.LogInformation("Executing optimization plan: {PlanName}", plan.Name);

                foreach (var action in plan.Actions.OrderBy(a => a.ExecutionOrder))
                {
                    try
                    {
                        switch (action.ActionType)
                        {
                            case OptimizationActionType.DeviceControl:
                                await ExecuteDeviceControlActionAsync(action);
                                break;

                            case OptimizationActionType.ScheduleChange:
                                await ExecuteScheduleChangeActionAsync(action);
                                break;

                            case OptimizationActionType.BatteryOperation:
                                await ExecuteBatteryOperationActionAsync(action);
                                break;

                            case OptimizationActionType.LoadShifting:
                                await ExecuteLoadShiftingActionAsync(action);
                                break;

                            case OptimizationActionType.ThermalAdjustment:
                                await ExecuteThermalAdjustmentActionAsync(action);
                                break;
                        }

                        action.ExecutionStatus = OptimizationActionStatus.Completed;
                        action.ExecutionTimestamp = DateTime.UtcNow;

                        // Small delay between actions to prevent system overload
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing optimization action: {ActionId}", action.ActionId);
                        action.ExecutionStatus = OptimizationActionStatus.Failed;
                        action.ErrorMessage = ex.Message;
                    }
                }

                plan.ExecutionStatus = plan.Actions.All(a => a.ExecutionStatus == OptimizationActionStatus.Completed) 
                    ? OptimizationPlanStatus.Completed 
                    : OptimizationPlanStatus.PartiallyCompleted;

                plan.ExecutedAt = DateTime.UtcNow;

                _logger.LogInformation("Optimization plan execution completed: {PlanName}. Status: {Status}", 
                    plan.Name, plan.ExecutionStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing optimization plan: {PlanName}", plan.Name);
                plan.ExecutionStatus = OptimizationPlanStatus.Failed;
                throw;
            }
        }

        public async Task<ComfortOptimizationResult> OptimizeComfortVsEfficiencyAsync(ComfortPreferences preferences)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                var comfortDevices = await context.Devices
                    .Where(d => d.Type == DeviceType.SmartThermostat ||
                               d.Type == DeviceType.SmartLight ||
                               d.Type == DeviceType.AirConditioner ||
                               d.Type == DeviceType.HeatPump)
                    .Where(d => d.Status == DeviceStatus.Active)
                    .ToListAsync();

                var optimizationActions = new List<ComfortOptimizationAction>();

                foreach (var device in comfortDevices)
                {
                    var currentSettings = await GetDeviceCurrentSettingsAsync(context, device.Id);
                    var optimalSettings = CalculateOptimalComfortSettings(device, preferences, currentSettings);

                    if (!AreSettingsEqual(currentSettings, optimalSettings))
                    {
                        optimizationActions.Add(new ComfortOptimizationAction
                        {
                            DeviceId = device.Id,
                            DeviceName = device.Name,
                            DeviceType = device.Type,
                            CurrentSettings = currentSettings,
                            OptimalSettings = optimalSettings,
                            ComfortImpact = CalculateComfortSettingsImpact(currentSettings, optimalSettings),
                            EfficiencyGain = CalculateEfficiencyGain(currentSettings, optimalSettings),
                            EnergySavings = CalculateSettingsEnergySavings(device, currentSettings, optimalSettings)
                        });
                    }
                }

                var totalEnergySavings = optimizationActions.Sum(a => a.EnergySavings);
                var averageComfortImpact = optimizationActions.Any() ? optimizationActions.Average(a => a.ComfortImpact) : 0;

                return new ComfortOptimizationResult
                {
                    GeneratedAt = DateTime.UtcNow,
                    UserPreferences = preferences,
                    Actions = optimizationActions,
                    TotalEnergySavings = totalEnergySavings,
                    AverageComfortImpact = averageComfortImpact,
                    ComfortEfficiencyRatio = totalEnergySavings / Math.Max(averageComfortImpact, 0.1),
                    Recommendations = GenerateComfortRecommendations(optimizationActions, preferences)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing comfort vs efficiency");
                throw;
            }
        }

        // Background service for continuous energy optimization
        private async Task<decimal> GetCurrentEnergyConsumptionAsync(NexusHomeDbContext context)
        {
            return await context.EnergyConsumptions
                .Where(e => e.Timestamp >= DateTime.UtcNow.AddMinutes(-15))
                .SumAsync(e => e.PowerConsumption);
        }

        private async Task<List<WeatherData>> GetWeatherForecastAsync(NexusHomeDbContext context, DateTime startDate, DateTime endDate)
        {
            return await context.WeatherData
                .Where(w => w.Timestamp >= startDate && w.Timestamp <= endDate)
                .Where(w => w.IsForecast || w.Timestamp >= DateTime.UtcNow.AddDays(-1))
                .OrderBy(w => w.Timestamp)
                .ToListAsync();
        }

        private async Task<BatteryStatus?> GetCurrentBatteryStatusAsync(NexusHomeDbContext context)
        {
            return await context.BatteryStatuses
                .OrderByDescending(b => b.Timestamp)
                .FirstOrDefaultAsync();
        }

        private async Task<List<SolarGenerationForecast>> ForecastSolarGenerationAsync(NexusHomeDbContext context, List<WeatherData> weatherForecast)
        {
            var forecast = new List<SolarGenerationForecast>();

            foreach (var weather in weatherForecast)
            {
                var solarIrradiance = weather.SolarIrradiance;
                var temperature = weather.Temperature;
                var cloudCover = weather.CloudCover;

                // Simplified solar generation calculation
                var baseGeneration = Math.Max(0, solarIrradiance * 0.15m); // 15% efficiency
                var temperatureAdjustment = 1 - (Math.Max(0, temperature - 25) * 0.004m); // -0.4% per degree above 25°C
                var cloudAdjustment = 1 - (cloudCover / 100 * 0.8m); // Up to 80% reduction for full cloud cover

                var predictedGeneration = baseGeneration * temperatureAdjustment * cloudAdjustment;

                forecast.Add(new SolarGenerationForecast
                {
                    Timestamp = weather.Timestamp,
                    PredictedGeneration = Math.Round(predictedGeneration, 4),
                    Confidence = 0.75 - (Math.Abs(weather.Timestamp - DateTime.UtcNow).TotalDays * 0.05), // Decreasing confidence over time
                    WeatherConditions = weather.Condition ?? "Unknown"
                });
            }

            return forecast;
        }

        private decimal GetCurrentEnergyRate()
        {
            var now = DateTime.Now.TimeOfDay;
            
            if (now >= _peakHoursStart && now <= _peakHoursEnd)
                return _energyRates["peak"];
            else if (now >= _offPeakStart || now <= _offPeakEnd)
                return _energyRates["offpeak"];
            else
                return _energyRates["standard"];
        }

        // Helper methods for calculations and optimizations...
        // (Implementation continues with specific optimization algorithms)

        private List<string> GenerateActionRecommendations(List<OptimizationStrategy> strategies)
        {
            var recommendations = new List<string>();

            var topStrategy = strategies.FirstOrDefault();
            if (topStrategy != null)
            {
                recommendations.Add($"Implement {topStrategy.Name} for maximum savings of ${topStrategy.PotentialSavings:F2}");
            }

            if (strategies.Any(s => s.Type == OptimizationType.LoadShifting))
            {
                recommendations.Add("Schedule high-energy appliances during off-peak hours");
            }

            if (strategies.Any(s => s.Type == OptimizationType.BatteryOptimization))
            {
                recommendations.Add("Optimize battery charging/discharging cycles based on energy rates");
            }

            return recommendations;
        }

        private decimal CalculateOverallComfortScore(List<OptimizationStrategy> strategies)
        {
            if (!strategies.Any()) return 1.0m;
            
            return 1.0m - strategies.Average(s => s.ComfortImpact / 10.0m);
        }

        private decimal CalculateEnvironmentalImpact(List<OptimizationStrategy> strategies)
        {
            // Simplified environmental impact calculation (CO2 reduction)
            return strategies.Sum(s => s.PotentialSavings * 0.5m); // 0.5 kg CO2 per kWh saved
        }

        // Additional helper methods would continue here...
    }

    // Background service for continuous optimization
    public class EnergyOptimizationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EnergyOptimizationBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        public EnergyOptimizationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<EnergyOptimizationBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMinutes(_configuration.GetValue<int>("EnergyManagement:OptimizationIntervalMinutes", 15));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var optimizationService = scope.ServiceProvider.GetRequiredService<IEnergyOptimizationService>();

                    // Run optimization every interval
                    var startTime = DateTime.UtcNow;
                    var endTime = startTime.AddHours(24);

                    var result = await optimizationService.OptimizeEnergyUsageAsync(startTime, endTime);

                    _logger.LogInformation("Energy optimization completed. Potential savings: ${Savings:F2}", 
                        result.EstimatedCostSavings);

                    // Execute high-priority optimization strategies automatically
                    var autoExecuteStrategies = result.Strategies
                        .Where(s => s.AutoExecute && s.ComfortImpact < 2.0)
                        .Take(3)
                        .ToList();

                    foreach (var strategy in autoExecuteStrategies)
                    {
                        try
                        {
                            var plan = ConvertStrategyToPlan(strategy);
                            await optimizationService.ExecuteOptimizationPlanAsync(plan);
                            _logger.LogInformation("Auto-executed optimization strategy: {StrategyName}", strategy.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error auto-executing optimization strategy: {StrategyName}", strategy.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in energy optimization background service");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }

        private OptimizationPlan ConvertStrategyToPlan(OptimizationStrategy strategy)
        {
            return new OptimizationPlan
            {
                Name = strategy.Name,
                Actions = new List<OptimizationAction>
                {
                    new OptimizationAction
                    {
                        ActionId = Guid.NewGuid().ToString(),
                        ActionType = OptimizationActionType.DeviceControl,
                        DeviceId = strategy.TargetDeviceId,
                        Parameters = strategy.Parameters,
                        ExecutionOrder = 1,
                        ExecutionStatus = OptimizationActionStatus.Pending
                    }
                },
                ExecutionStatus = OptimizationPlanStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    // Data classes for energy optimization
    public class OptimizationResult
    {
        public DateTime OptimizationTimestamp { get; set; }
        public DateRange OptimizationPeriod { get; set; } = new();
        public decimal CurrentConsumption { get; set; }
        public List<OptimizationStrategy> Strategies { get; set; } = new();
        public decimal TotalPotentialSavings { get; set; }
        public decimal EstimatedCostSavings { get; set; }
        public decimal ComfortScore { get; set; }
        public decimal EnvironmentalImpact { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
        public List<OptimizationStrategy> ImplementationPriority { get; set; } = new();
    }

    public class OptimizationStrategy
    {
        public string Name { get; set; } = string.Empty;
        public OptimizationType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal PotentialSavings { get; set; }
        public double ComfortImpact { get; set; } // 0-10 scale
        public double ImplementationComplexity { get; set; } // 0-10 scale
        public bool AutoExecute { get; set; }
        public int? TargetDeviceId { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    // Additional data classes would continue here...
    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    // Enums for optimization
    public enum OptimizationActionType
    {
        DeviceControl,
        ScheduleChange,
        BatteryOperation,
        LoadShifting,
        ThermalAdjustment
    }

    public enum OptimizationActionStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    public enum OptimizationPlanStatus
    {
        Pending,
        InProgress,
        Completed,
        PartiallyCompleted,
        Failed,
        Cancelled
    }

    // More data classes would be defined here for complete implementation...
}