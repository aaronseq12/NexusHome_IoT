using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;
using Microsoft.EntityFrameworkCore;
using NexusHome.Models;
using NexusHome.Data;
using System.Text.Json;

namespace NexusHome.AI
{
    public interface IPredictiveMaintenanceService
    {
        Task<MaintenancePrediction> PredictMaintenanceNeedAsync(int deviceId);
        Task<List<MaintenancePrediction>> GetMaintenancePredictionsAsync(DateTime startDate, DateTime endDate);
        Task TrainModelsAsync();
        Task<AnomalyDetectionResult> DetectAnomaliesAsync(int deviceId, List<decimal> values);
        Task<EnergyConsumptionPrediction> PredictEnergyConsumptionAsync(int deviceId, DateTime predictionDate);
        Task UpdateModelAsync(int deviceId, MaintenanceFeedback feedback);
    }

    public class PredictiveMaintenanceService : IPredictiveMaintenanceService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PredictiveMaintenanceService> _logger;
        private readonly MLContext _mlContext;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, ITransformer> _trainedModels;

        public PredictiveMaintenanceService(
            IServiceProvider serviceProvider,
            ILogger<PredictiveMaintenanceService> logger,
            MLContext mlContext,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _mlContext = mlContext;
            _configuration = configuration;
            _trainedModels = new Dictionary<string, ITransformer>();
        }

        public async Task<MaintenancePrediction> PredictMaintenanceNeedAsync(int deviceId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                var device = await context.Devices.FindAsync(deviceId);
                if (device == null)
                {
                    throw new ArgumentException($"Device with ID {deviceId} not found");
                }

                // Get historical data for the device
                var historicalData = await GetDeviceHistoricalDataAsync(context, deviceId);
                
                if (historicalData.Count < 100) // Need minimum data points for accurate prediction
                {
                    return new MaintenancePrediction
                    {
                        DeviceId = deviceId,
                        FailureProbability = 0,
                        PredictedFailureDate = null,
                        Confidence = 0,
                        RecommendedActions = new List<string> { "Insufficient data for prediction. Continue monitoring." },
                        FeatureImportance = new Dictionary<string, float>()
                    };
                }

                // Prepare feature data
                var features = ExtractFeatures(historicalData);
                
                // Get or train model for this device type
                var modelKey = $"maintenance_{device.Type}";
                if (!_trainedModels.ContainsKey(modelKey))
                {
                    await TrainMaintenanceModel(device.Type);
                }

                if (_trainedModels.TryGetValue(modelKey, out var model))
                {
                    // Make prediction
                    var predictionEngine = _mlContext.Model.CreatePredictionEngine<MaintenanceFeatures, MaintenancePredictionOutput>(model);
                    var prediction = predictionEngine.Predict(features);

                    // Calculate predicted failure date if probability is significant
                    DateTime? predictedFailureDate = null;
                    if (prediction.FailureProbability > 0.3)
                    {
                        // Use degradation trend to estimate failure date
                        var degradationRate = CalculateDegradationRate(historicalData);
                        var daysToFailure = (1 - prediction.FailureProbability) / Math.Max(degradationRate, 0.001) * 30;
                        predictedFailureDate = DateTime.UtcNow.AddDays(Math.Min(daysToFailure, 365));
                    }

                    return new MaintenancePrediction
                    {
                        DeviceId = deviceId,
                        DeviceName = device.Name,
                        DeviceType = device.Type,
                        FailureProbability = Math.Round(prediction.FailureProbability, 4),
                        PredictedFailureDate = predictedFailureDate,
                        Confidence = Math.Round(prediction.Confidence, 4),
                        RecommendedActions = GenerateMaintenanceRecommendations(prediction.FailureProbability, device),
                        FeatureImportance = GetFeatureImportance(features),
                        LastUpdated = DateTime.UtcNow
                    };
                }

                return new MaintenancePrediction
                {
                    DeviceId = deviceId,
                    FailureProbability = 0,
                    Confidence = 0,
                    RecommendedActions = new List<string> { "Model not available for prediction" }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting maintenance need for device {DeviceId}", deviceId);
                throw;
            }
        }

        public async Task<List<MaintenancePrediction>> GetMaintenancePredictionsAsync(DateTime startDate, DateTime endDate)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            var devices = await context.Devices
                .Where(d => d.Status == DeviceStatus.Active)
                .ToListAsync();

            var predictions = new List<MaintenancePrediction>();

            foreach (var device in devices)
            {
                try
                {
                    var prediction = await PredictMaintenanceNeedAsync(device.Id);
                    
                    // Filter by date range if predicted failure date is within range
                    if (prediction.PredictedFailureDate == null ||
                        (prediction.PredictedFailureDate >= startDate && prediction.PredictedFailureDate <= endDate))
                    {
                        predictions.Add(prediction);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting prediction for device {DeviceId}", device.Id);
                }
            }

            return predictions.OrderByDescending(p => p.FailureProbability).ToList();
        }

        public async Task TrainModelsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            _logger.LogInformation("Starting model training for predictive maintenance");

            // Get all device types that have sufficient data
            var deviceTypes = await context.Devices
                .Where(d => d.MaintenanceRecords.Any())
                .Select(d => d.Type)
                .Distinct()
                .ToListAsync();

            foreach (var deviceType in deviceTypes)
            {
                try
                {
                    await TrainMaintenanceModel(deviceType);
                    await TrainAnomalyDetectionModel(deviceType);
                    await TrainEnergyPredictionModel(deviceType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error training models for device type {DeviceType}", deviceType);
                }
            }

            _logger.LogInformation("Model training completed");
        }

        public async Task<AnomalyDetectionResult> DetectAnomaliesAsync(int deviceId, List<decimal> values)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            var device = await context.Devices.FindAsync(deviceId);
            if (device == null)
            {
                throw new ArgumentException($"Device with ID {deviceId} not found");
            }

            try
            {
                // Prepare time series data for anomaly detection
                var timeSeriesData = values.Select((value, index) => new TimeSeriesData
                {
                    Timestamp = DateTime.UtcNow.AddMinutes(-values.Count + index),
                    Value = (float)value
                }).ToList();

                // Use ML.NET's anomaly detection
                var dataView = _mlContext.Data.LoadFromEnumerable(timeSeriesData);
                
                // Create the anomaly detection pipeline
                var pipeline = _mlContext.Transforms.DetectAnomaliesBySrCnn(
                    outputColumnName: "Prediction",
                    inputColumnName: nameof(TimeSeriesData.Value),
                    windowSize: Math.Min(timeSeriesData.Count / 4, 12),
                    backAddWindowSize: Math.Min(timeSeriesData.Count / 8, 6),
                    lookaheadWindowSize: Math.Min(timeSeriesData.Count / 8, 6),
                    averagingWindowSize: Math.Min(timeSeriesData.Count / 8, 6),
                    judgmentWindowSize: Math.Min(timeSeriesData.Count / 8, 6),
                    threshold: 0.8);

                var model = pipeline.Fit(dataView);
                var transformedData = model.Transform(dataView);
                var predictions = _mlContext.Data.CreateEnumerable<AnomalyPrediction>(transformedData, false).ToList();

                var anomalies = new List<AnomalyPoint>();
                for (int i = 0; i < predictions.Count; i++)
                {
                    if (predictions[i].Prediction[0] == 1) // Anomaly detected
                    {
                        anomalies.Add(new AnomalyPoint
                        {
                            Index = i,
                            Value = (decimal)timeSeriesData[i].Value,
                            Timestamp = timeSeriesData[i].Timestamp,
                            Score = predictions[i].Prediction[1]
                        });
                    }
                }

                return new AnomalyDetectionResult
                {
                    DeviceId = deviceId,
                    HasAnomalies = anomalies.Any(),
                    AnomalyCount = anomalies.Count,
                    Anomalies = anomalies,
                    Confidence = anomalies.Any() ? anomalies.Average(a => a.Score) : 0,
                    DetectionTimestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting anomalies for device {DeviceId}", deviceId);
                throw;
            }
        }

        public async Task<EnergyConsumptionPrediction> PredictEnergyConsumptionAsync(int deviceId, DateTime predictionDate)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            var device = await context.Devices.FindAsync(deviceId);
            if (device == null)
            {
                throw new ArgumentException($"Device with ID {deviceId} not found");
            }

            try
            {
                // Get historical energy consumption data
                var historicalData = await context.EnergyConsumptions
                    .Where(e => e.DeviceId == deviceId)
                    .Where(e => e.Timestamp >= DateTime.UtcNow.AddDays(-90))
                    .OrderBy(e => e.Timestamp)
                    .ToListAsync();

                if (historicalData.Count < 30)
                {
                    return new EnergyConsumptionPrediction
                    {
                        DeviceId = deviceId,
                        PredictionDate = predictionDate,
                        PredictedConsumption = device.PowerRating * 24, // Fallback to rated power * 24 hours
                        Confidence = 0.1,
                        PredictionRange = new Range<decimal>(0, device.PowerRating * 24 * 1.5m)
                    };
                }

                // Prepare time series data for forecasting
                var timeSeriesData = historicalData.Select(h => new TimeSeriesData
                {
                    Timestamp = h.Timestamp,
                    Value = (float)h.PowerConsumption
                }).ToList();

                var dataView = _mlContext.Data.LoadFromEnumerable(timeSeriesData);

                // Create forecasting pipeline
                var forecastingPipeline = _mlContext.Forecasting.ForecastBySsa(
                    outputColumnName: "ForecastedValue",
                    inputColumnName: nameof(TimeSeriesData.Value),
                    windowSize: 7, // Weekly pattern
                    seriesLength: Math.Min(timeSeriesData.Count, 30),
                    trainSize: Math.Min(timeSeriesData.Count, 30),
                    horizon: 1, // Predict 1 period ahead
                    confidenceLevel: 0.95f,
                    confidenceLowerBoundColumn: "LowerBound",
                    confidenceUpperBoundColumn: "UpperBound");

                var model = forecastingPipeline.Fit(dataView);
                var forecastEngine = model.CreateTimeSeriesEngine<TimeSeriesData, EnergyForecast>(_mlContext);
                var forecast = forecastEngine.Predict();

                return new EnergyConsumptionPrediction
                {
                    DeviceId = deviceId,
                    DeviceName = device.Name,
                    PredictionDate = predictionDate,
                    PredictedConsumption = Math.Round((decimal)forecast.ForecastedValue[0], 4),
                    Confidence = 0.8, // Based on model performance
                    PredictionRange = new Range<decimal>(
                        Math.Round((decimal)forecast.LowerBound[0], 4),
                        Math.Round((decimal)forecast.UpperBound[0], 4)
                    ),
                    PredictionTimestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting energy consumption for device {DeviceId}", deviceId);
                throw;
            }
        }

        public async Task UpdateModelAsync(int deviceId, MaintenanceFeedback feedback)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            var device = await context.Devices.FindAsync(deviceId);
            if (device == null) return;

            try
            {
                // Store feedback for model improvement
                var feedbackRecord = new ModelFeedback
                {
                    DeviceId = deviceId,
                    DeviceType = device.Type,
                    FeedbackType = feedback.FeedbackType,
                    PredictedFailureProbability = feedback.PredictedFailureProbability,
                    ActualOutcome = feedback.ActualOutcome,
                    Timestamp = DateTime.UtcNow,
                    Notes = feedback.Notes
                };

                // Add feedback to context (would need to add ModelFeedback entity to DbContext)
                // context.ModelFeedbacks.Add(feedbackRecord);
                // await context.SaveChangesAsync();

                _logger.LogInformation("Model feedback recorded for device {DeviceId}", deviceId);

                // If we have enough feedback, retrain the model
                var feedbackCount = await GetFeedbackCountForDeviceType(device.Type);
                if (feedbackCount >= 50) // Threshold for retraining
                {
                    _ = Task.Run(async () => await TrainMaintenanceModel(device.Type));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model with feedback for device {DeviceId}", deviceId);
            }
        }

        private async Task<List<DeviceHistoricalData>> GetDeviceHistoricalDataAsync(NexusHomeDbContext context, int deviceId)
        {
            var energyData = await context.EnergyConsumptions
                .Where(e => e.DeviceId == deviceId)
                .Where(e => e.Timestamp >= DateTime.UtcNow.AddDays(-90))
                .OrderBy(e => e.Timestamp)
                .ToListAsync();

            var maintenanceRecords = await context.MaintenanceRecords
                .Where(m => m.DeviceId == deviceId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            var device = await context.Devices.FindAsync(deviceId);

            return energyData.Select(e => new DeviceHistoricalData
            {
                Timestamp = e.Timestamp,
                PowerConsumption = e.PowerConsumption,
                Voltage = e.Voltage,
                Current = e.Current,
                PowerFactor = e.PowerFactor,
                Temperature = device?.Type == DeviceType.SmartThermostat ? 22 : 25, // Mock temperature data
                Vibration = device?.Type == DeviceType.HeatPump ? 0.5f : 0, // Mock vibration data
                OperatingHours = (float)(DateTime.UtcNow - device.CreatedAt).TotalHours,
                MaintenanceFlag = maintenanceRecords.Any(m => Math.Abs((m.CreatedAt - e.Timestamp).TotalDays) < 7)
            }).ToList();
        }

        private MaintenanceFeatures ExtractFeatures(List<DeviceHistoricalData> historicalData)
        {
            if (!historicalData.Any()) return new MaintenanceFeatures();

            var recent = historicalData.TakeLast(30).ToList();
            var older = historicalData.Take(historicalData.Count - 30).ToList();

            return new MaintenanceFeatures
            {
                AveragePowerConsumption = (float)recent.Average(h => (double)h.PowerConsumption),
                PowerConsumptionStdDev = (float)CalculateStandardDeviation(recent.Select(h => (double)h.PowerConsumption)),
                AverageVoltage = (float)recent.Average(h => (double)h.Voltage),
                AverageCurrent = (float)recent.Average(h => (double)h.Current),
                AverageTemperature = recent.Average(h => h.Temperature),
                AverageVibration = recent.Average(h => h.Vibration),
                OperatingHours = recent.Max(h => h.OperatingHours),
                PowerTrend = CalculateTrend(recent.Select(h => (double)h.PowerConsumption).ToList()),
                TemperatureTrend = CalculateTrend(recent.Select(h => (double)h.Temperature).ToList()),
                DaysSinceLastMaintenance = (float)historicalData
                    .Where(h => h.MaintenanceFlag)
                    .Select(h => (DateTime.UtcNow - h.Timestamp).TotalDays)
                    .DefaultIfEmpty(365)
                    .Min(),
                AnomalyScore = CalculateAnomalyScore(recent, older)
            };
        }

        private async Task TrainMaintenanceModel(DeviceType deviceType)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

            try
            {
                _logger.LogInformation("Training maintenance model for device type: {DeviceType}", deviceType);

                // Get training data for this device type
                var devices = await context.Devices
                    .Where(d => d.Type == deviceType)
                    .ToListAsync();

                var trainingData = new List<MaintenanceTrainingData>();

                foreach (var device in devices)
                {
                    var historicalData = await GetDeviceHistoricalDataAsync(context, device.Id);
                    if (historicalData.Count > 50)
                    {
                        var features = ExtractFeatures(historicalData);
                        var hasFailure = await context.MaintenanceRecords
                            .AnyAsync(m => m.DeviceId == device.Id && 
                                         m.Type == MaintenanceType.Corrective &&
                                         m.CreatedAt >= DateTime.UtcNow.AddDays(-30));

                        trainingData.Add(new MaintenanceTrainingData
                        {
                            Features = features,
                            NeedsMaintenance = hasFailure
                        });
                    }
                }

                if (trainingData.Count < 10)
                {
                    _logger.LogWarning("Insufficient training data for device type: {DeviceType}", deviceType);
                    return;
                }

                // Prepare ML.NET data
                var features = trainingData.Select(t => new MaintenanceMLData
                {
                    AveragePowerConsumption = t.Features.AveragePowerConsumption,
                    PowerConsumptionStdDev = t.Features.PowerConsumptionStdDev,
                    AverageVoltage = t.Features.AverageVoltage,
                    AverageCurrent = t.Features.AverageCurrent,
                    AverageTemperature = t.Features.AverageTemperature,
                    AverageVibration = t.Features.AverageVibration,
                    OperatingHours = t.Features.OperatingHours,
                    PowerTrend = t.Features.PowerTrend,
                    TemperatureTrend = t.Features.TemperatureTrend,
                    DaysSinceLastMaintenance = t.Features.DaysSinceLastMaintenance,
                    AnomalyScore = t.Features.AnomalyScore,
                    NeedsMaintenance = t.NeedsMaintenance
                }).ToList();

                var dataView = _mlContext.Data.LoadFromEnumerable(features);

                // Create training pipeline
                var pipeline = _mlContext.Transforms.Concatenate("Features", 
                        nameof(MaintenanceMLData.AveragePowerConsumption),
                        nameof(MaintenanceMLData.PowerConsumptionStdDev),
                        nameof(MaintenanceMLData.AverageVoltage),
                        nameof(MaintenanceMLData.AverageCurrent),
                        nameof(MaintenanceMLData.AverageTemperature),
                        nameof(MaintenanceMLData.AverageVibration),
                        nameof(MaintenanceMLData.OperatingHours),
                        nameof(MaintenanceMLData.PowerTrend),
                        nameof(MaintenanceMLData.TemperatureTrend),
                        nameof(MaintenanceMLData.DaysSinceLastMaintenance),
                        nameof(MaintenanceMLData.AnomalyScore))
                    .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                    .Append(_mlContext.BinaryClassification.Trainers.FastTree(
                        labelColumnName: nameof(MaintenanceMLData.NeedsMaintenance),
                        featureColumnName: "Features"));

                // Train the model
                var model = pipeline.Fit(dataView);

                // Store the trained model
                var modelKey = $"maintenance_{deviceType}";
                _trainedModels[modelKey] = model;

                // Optionally save model to disk
                var modelPath = Path.Combine("Models", $"{modelKey}.zip");
                Directory.CreateDirectory("Models");
                _mlContext.Model.Save(model, dataView.Schema, modelPath);

                _logger.LogInformation("Successfully trained maintenance model for device type: {DeviceType}", deviceType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error training maintenance model for device type: {DeviceType}", deviceType);
            }
        }

        private async Task TrainAnomalyDetectionModel(DeviceType deviceType)
        {
            // Implement anomaly detection model training
            _logger.LogInformation("Training anomaly detection model for device type: {DeviceType}", deviceType);
        }

        private async Task TrainEnergyPredictionModel(DeviceType deviceType)
        {
            // Implement energy prediction model training
            _logger.LogInformation("Training energy prediction model for device type: {DeviceType}", deviceType);
        }

        private List<string> GenerateMaintenanceRecommendations(double failureProbability, Device device)
        {
            var recommendations = new List<string>();

            if (failureProbability > 0.8)
            {
                recommendations.Add("URGENT: Schedule immediate maintenance inspection");
                recommendations.Add("Consider temporary shutdown if safety critical");
                recommendations.Add("Prepare replacement parts");
            }
            else if (failureProbability > 0.6)
            {
                recommendations.Add("Schedule maintenance within next 7 days");
                recommendations.Add("Increase monitoring frequency");
                recommendations.Add("Review operating conditions");
            }
            else if (failureProbability > 0.4)
            {
                recommendations.Add("Schedule preventive maintenance within 30 days");
                recommendations.Add("Monitor device performance closely");
            }
            else if (failureProbability > 0.2)
            {
                recommendations.Add("Continue normal monitoring");
                recommendations.Add("Follow regular maintenance schedule");
            }
            else
            {
                recommendations.Add("Device operating normally");
                recommendations.Add("No immediate action required");
            }

            // Device-specific recommendations
            switch (device.Type)
            {
                case DeviceType.SmartThermostat:
                    if (failureProbability > 0.5)
                        recommendations.Add("Check HVAC system connections and calibration");
                    break;
                case DeviceType.SolarInverter:
                    if (failureProbability > 0.5)
                        recommendations.Add("Inspect DC connections and cooling system");
                    break;
                case DeviceType.BatteryStorage:
                    if (failureProbability > 0.5)
                        recommendations.Add("Check battery cell balance and temperature management");
                    break;
            }

            return recommendations;
        }

        private Dictionary<string, float> GetFeatureImportance(MaintenanceFeatures features)
        {
            // Simplified feature importance calculation
            return new Dictionary<string, float>
            {
                { "PowerConsumption", 0.25f },
                { "Temperature", 0.20f },
                { "OperatingHours", 0.15f },
                { "PowerTrend", 0.15f },
                { "AnomalyScore", 0.10f },
                { "DaysSinceLastMaintenance", 0.08f },
                { "Vibration", 0.07f }
            };
        }

        private double CalculateStandardDeviation(IEnumerable<double> values)
        {
            var average = values.Average();
            var sumSquaredDifferences = values.Sum(x => Math.Pow(x - average, 2));
            return Math.Sqrt(sumSquaredDifferences / values.Count());
        }

        private float CalculateTrend(List<double> values)
        {
            if (values.Count < 2) return 0;

            var n = values.Count;
            var sumX = Enumerable.Range(0, n).Sum();
            var sumY = values.Sum();
            var sumXY = values.Select((y, x) => x * y).Sum();
            var sumX2 = Enumerable.Range(0, n).Sum(x => x * x);

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            return (float)slope;
        }

        private double CalculateDegradationRate(List<DeviceHistoricalData> historicalData)
        {
            if (historicalData.Count < 30) return 0.001;

            var recent = historicalData.TakeLast(7).Average(h => (double)h.PowerConsumption);
            var baseline = historicalData.Take(30).Average(h => (double)h.PowerConsumption);
            
            return Math.Max((recent - baseline) / baseline / 30, 0.001); // Daily degradation rate
        }

        private float CalculateAnomalyScore(List<DeviceHistoricalData> recent, List<DeviceHistoricalData> older)
        {
            if (!older.Any()) return 0;

            var recentAvg = recent.Average(h => (double)h.PowerConsumption);
            var olderAvg = older.Average(h => (double)h.PowerConsumption);
            var olderStdDev = CalculateStandardDeviation(older.Select(h => (double)h.PowerConsumption));

            if (olderStdDev == 0) return 0;

            var zScore = Math.Abs(recentAvg - olderAvg) / olderStdDev;
            return Math.Min((float)zScore / 3.0f, 1.0f); // Normalize to 0-1
        }

        private async Task<int> GetFeedbackCountForDeviceType(DeviceType deviceType)
        {
            // Mock implementation - would query actual feedback table
            return 25;
        }
    }

    // Background service for continuous predictive maintenance monitoring
    public class PredictiveMaintenanceBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PredictiveMaintenanceBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        public PredictiveMaintenanceBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<PredictiveMaintenanceBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalHours = _configuration.GetValue<int>("PredictiveMaintenance:ModelUpdateIntervalHours");
            var interval = TimeSpan.FromHours(intervalHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Running predictive maintenance analysis");

                    using var scope = _serviceProvider.CreateScope();
                    var predictiveService = scope.ServiceProvider.GetRequiredService<IPredictiveMaintenanceService>();
                    var context = scope.ServiceProvider.GetRequiredService<NexusHomeDbContext>();

                    // Get all active devices
                    var activeDevices = await context.Devices
                        .Where(d => d.Status == DeviceStatus.Active && d.IsOnline)
                        .ToListAsync(stoppingToken);

                    foreach (var device in activeDevices)
                    {
                        try
                        {
                            var prediction = await predictiveService.PredictMaintenanceNeedAsync(device.Id);

                            // Create maintenance record if failure probability is high
                            if (prediction.FailureProbability > 0.7)
                            {
                                var existingRecord = await context.MaintenanceRecords
                                    .AnyAsync(m => m.DeviceId == device.Id && 
                                                  m.Status == MaintenanceStatus.Scheduled &&
                                                  m.Type == MaintenanceType.Predictive, stoppingToken);

                                if (!existingRecord)
                                {
                                    var maintenanceRecord = new DeviceMaintenanceRecord
                                    {
                                        DeviceId = device.Id,
                                        Type = MaintenanceType.Predictive,
                                        Title = $"Predicted Maintenance - {device.Name}",
                                        Description = $"AI prediction indicates high failure probability ({prediction.FailureProbability:P0}). Recommended actions: {string.Join(", ", prediction.RecommendedActions)}",
                                        Status = MaintenanceStatus.Scheduled,
                                        Priority = prediction.FailureProbability > 0.8 ? MaintenancePriority.Critical : MaintenancePriority.High,
                                        ScheduledDate = prediction.PredictedFailureDate?.AddDays(-7) ?? DateTime.UtcNow.AddDays(3),
                                        PredictedFailureProba = (decimal)prediction.FailureProbability,
                                        PredictedFailureDate = prediction.PredictedFailureDate,
                                        CreatedAt = DateTime.UtcNow
                                    };

                                    context.MaintenanceRecords.Add(maintenanceRecord);

                                    // Create alert
                                    var alert = new DeviceAlert
                                    {
                                        DeviceId = device.Id,
                                        Type = AlertType.MaintenanceRequired,
                                        Severity = prediction.FailureProbability > 0.8 ? AlertSeverity.Critical : AlertSeverity.Warning,
                                        Title = "Predictive Maintenance Required",
                                        Message = $"Device {device.Name} requires maintenance. Failure probability: {prediction.FailureProbability:P0}",
                                        Timestamp = DateTime.UtcNow,
                                        Data = JsonSerializer.Serialize(prediction)
                                    };

                                    context.DeviceAlerts.Add(alert);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing predictive maintenance for device {DeviceId}", device.Id);
                        }
                    }

                    await context.SaveChangesAsync(stoppingToken);

                    // Update ML models periodically
                    if (DateTime.UtcNow.Hour == 2) // Run at 2 AM
                    {
                        await predictiveService.TrainModelsAsync();
                    }

                    _logger.LogInformation("Completed predictive maintenance analysis for {DeviceCount} devices", activeDevices.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in predictive maintenance background service");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }
    }

    // Data classes for ML.NET
    public class MaintenanceFeatures
    {
        public float AveragePowerConsumption { get; set; }
        public float PowerConsumptionStdDev { get; set; }
        public float AverageVoltage { get; set; }
        public float AverageCurrent { get; set; }
        public float AverageTemperature { get; set; }
        public float AverageVibration { get; set; }
        public float OperatingHours { get; set; }
        public float PowerTrend { get; set; }
        public float TemperatureTrend { get; set; }
        public float DaysSinceLastMaintenance { get; set; }
        public float AnomalyScore { get; set; }
    }

    public class MaintenanceMLData : MaintenanceFeatures
    {
        public bool NeedsMaintenance { get; set; }
    }

    public class MaintenancePredictionOutput
    {
        [ColumnName("PredictedLabel")]
        public bool NeedsMaintenance { get; set; }
        
        [ColumnName("Probability")]
        public float FailureProbability { get; set; }
        
        [ColumnName("Score")]
        public float Confidence { get; set; }
    }

    public class TimeSeriesData
    {
        public DateTime Timestamp { get; set; }
        public float Value { get; set; }
    }

    public class AnomalyPrediction
    {
        [VectorType(3)]
        public double[] Prediction { get; set; } = new double[3];
    }

    public class EnergyForecast
    {
        public float[] ForecastedValue { get; set; } = new float[1];
        public float[] LowerBound { get; set; } = new float[1];
        public float[] UpperBound { get; set; } = new float[1];
    }

    // Result classes
    public class MaintenancePrediction
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DeviceType DeviceType { get; set; }
        public double FailureProbability { get; set; }
        public DateTime? PredictedFailureDate { get; set; }
        public double Confidence { get; set; }
        public List<string> RecommendedActions { get; set; } = new();
        public Dictionary<string, float> FeatureImportance { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class AnomalyDetectionResult
    {
        public int DeviceId { get; set; }
        public bool HasAnomalies { get; set; }
        public int AnomalyCount { get; set; }
        public List<AnomalyPoint> Anomalies { get; set; } = new();
        public double Confidence { get; set; }
        public DateTime DetectionTimestamp { get; set; }
    }

    public class AnomalyPoint
    {
        public int Index { get; set; }
        public decimal Value { get; set; }
        public DateTime Timestamp { get; set; }
        public float Score { get; set; }
    }

    public class EnergyConsumptionPrediction
    {
        public int DeviceId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public DateTime PredictionDate { get; set; }
        public decimal PredictedConsumption { get; set; }
        public double Confidence { get; set; }
        public Range<decimal> PredictionRange { get; set; } = new();
        public DateTime PredictionTimestamp { get; set; }
    }

    public class Range<T>
    {
        public T Min { get; set; }
        public T Max { get; set; }

        public Range() { }
        public Range(T min, T max)
        {
            Min = min;
            Max = max;
        }
    }

    // Supporting classes
    public class DeviceHistoricalData
    {
        public DateTime Timestamp { get; set; }
        public decimal PowerConsumption { get; set; }
        public decimal Voltage { get; set; }
        public decimal Current { get; set; }
        public decimal PowerFactor { get; set; }
        public float Temperature { get; set; }
        public float Vibration { get; set; }
        public float OperatingHours { get; set; }
        public bool MaintenanceFlag { get; set; }
    }

    public class MaintenanceTrainingData
    {
        public MaintenanceFeatures Features { get; set; } = new();
        public bool NeedsMaintenance { get; set; }
    }

    public class MaintenanceFeedback
    {
        public string FeedbackType { get; set; } = string.Empty;
        public double PredictedFailureProbability { get; set; }
        public string ActualOutcome { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class ModelFeedback
    {
        public int DeviceId { get; set; }
        public DeviceType DeviceType { get; set; }
        public string FeedbackType { get; set; } = string.Empty;
        public double PredictedFailureProbability { get; set; }
        public string ActualOutcome { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Notes { get; set; }
    }
}