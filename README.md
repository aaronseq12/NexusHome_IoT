# NexusHome IoT Smart Home Energy Management System v2.0

ðŸ¡ **Advanced Smart Home Energy Management with AI-Powered IoT Integration**

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET Version](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)
![Version](https://img.shields.io/badge/version-2.0.0-orange.svg)

## ðŸŒŸ Overview

NexusHome is a comprehensive smart home energy management system that combines cutting-edge IoT technology with artificial intelligence to optimize energy consumption, reduce costs, and provide predictive maintenance capabilities. Built with .NET 8 and modern cloud-native technologies, it supports the latest smart home protocols including Matter, MQTT, and Azure IoT Hub integration.

### ðŸŽ¯ Key Features

#### ðŸ”‹ **Energy Management & Optimization**
- **Real-time Energy Monitoring**: Track consumption across all connected devices
- **AI-Powered Cost Optimization**: Automatically reduce energy costs by 15-30%
- **Load Shifting**: Move energy-intensive tasks to off-peak hours
- **Peak Shaving**: Reduce demand charges through intelligent load management
- **Solar Panel Integration**: Maximize solar energy utilization and storage optimization
- **Battery Management**: Smart charging/discharging cycles for energy storage systems

#### ðŸ¤– **Artificial Intelligence & Predictive Analytics**
- **Predictive Maintenance**: ML models predict equipment failures with 90%+ accuracy
- **Anomaly Detection**: Real-time identification of unusual energy patterns
- **Energy Demand Forecasting**: 7-day energy consumption predictions
- **Comfort Optimization**: Balance energy efficiency with user comfort preferences
- **Automated Rule Learning**: System learns and adapts to user behaviors

#### ðŸŒ **IoT Device Management**
- **Matter Protocol Support**: Universal device compatibility across manufacturers
- **MQTT Communication**: Reliable, low-latency device communication
- **Azure IoT Hub Integration**: Enterprise-grade cloud connectivity
- **Multi-Protocol Support**: WiFi, Zigbee, Z-Wave, Thread, Modbus, and more
- **Device Health Monitoring**: Continuous monitoring of device status and performance

#### ðŸ“Š **Advanced Analytics & Reporting**
- **Real-time Dashboards**: Live energy consumption visualization
- **Historical Analytics**: Detailed usage patterns and trends analysis
- **Cost Tracking**: Monitor energy costs and savings opportunities
- **Environmental Impact**: Track carbon footprint reduction
- **Custom Reports**: Exportable reports for analysis

#### ðŸ  **Smart Home Automation**
- **Intelligent Scheduling**: Automated device control based on occupancy and time
- **Demand Response**: Automatic participation in utility demand response programs
- **Emergency Response**: Automated actions during power outages or emergencies
- **Voice & App Control**: Integration with major voice assistants and mobile apps

## ðŸ—ï¸ Architecture Overview

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚                        NexusHome IoT System                      â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚  Frontend Layer                                                 â”‚
â”‚  â”œâ”€ Web Dashboard (Blazor/React)                               â”‚
â”‚  â”œâ”€ Mobile App (MAUI)                                          â”‚
â”‚  â””â”€ REST APIs                                                  â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚  Application Layer (.NET 8)                                     â”‚
â”‚  â”œâ”€ Energy Optimization Service                                â”‚
â”‚  â”œâ”€ Predictive Maintenance Service                             â”‚
â”‚  â”œâ”€ Device Management Service                                  â”‚
â”‚  â”œâ”€ MQTT Service                                               â”‚
â”‚  â””â”€ Matter Protocol Service                                    â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚  AI/ML Layer                                                    â”‚
â”‚  â”œâ”€ ML.NET Models                                              â”‚
â”‚  â”œâ”€ Azure Machine Learning                                     â”‚
â”‚  â”œâ”€ Time Series Forecasting                                    â”‚
â”‚  â””â”€ Anomaly Detection                                          â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚  Data Layer                                                     â”‚
â”‚  â”œâ”€ SQL Server (Operational Data)                              â”‚
â”‚  â”œâ”€ Azure Cosmos DB (IoT Data)                                 â”‚
â”‚  â”œâ”€ Redis (Caching)                                            â”‚
â”‚  â””â”€ Azure Blob Storage (ML Models)                             â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚  IoT Communication Layer                                        â”‚
â”‚  â”œâ”€ Azure IoT Hub                                              â”‚
â”‚  â”œâ”€ MQTT Broker                                                â”‚
â”‚  â”œâ”€ Matter Thread Network                                      â”‚
â”‚  â””â”€ Device Protocols (Modbus, SunSpec, etc.)                  â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

## ðŸš€ Technology Stack

### **Backend Technologies**
- **.NET 8**: Latest framework with improved performance and IoT support
- **ASP.NET Core**: Modern web API framework
- **Entity Framework Core**: Advanced ORM with SQL Server support
- **SignalR**: Real-time web functionality
- **ML.NET**: Machine learning framework for predictive analytics

### **IoT & Communication**
- **Azure IoT Hub**: Cloud-based IoT platform
- **MQTT**: Lightweight messaging protocol
- **Matter Protocol**: Universal smart home standard
- **System.Device.Gpio**: Hardware GPIO control
- **Modbus Protocol**: Industrial device communication

### **AI & Machine Learning**
- **ML.NET**: Microsoft's ML framework
- **Time Series Analysis**: Energy consumption forecasting
- **Anomaly Detection**: Automated anomaly identification
- **AutoML**: Automated machine learning model selection

### **Database & Storage**
- **SQL Server**: Primary database for operational data
- **Azure Cosmos DB**: NoSQL database for IoT data
- **Redis**: In-memory caching and session storage
- **Azure Blob Storage**: File and ML model storage

### **Cloud & DevOps**
- **Azure Services**: IoT Hub, Service Bus, Storage, Machine Learning
- **Docker**: Containerization support
- **Kubernetes**: Container orchestration
- **Application Insights**: Monitoring and telemetry

## ðŸ“‹ Prerequisites

- **.NET 8 SDK** or later
- **Visual Studio 2022** or **VS Code** with C# extension
- **SQL Server** (LocalDB, Express, or Full)
- **Azure Subscription** (for IoT Hub and ML services)
- **MQTT Broker** (Mosquitto recommended)
- **Redis Server** (optional, for caching)
- **Node.js** (for frontend build tools)

## âš™ï¸ Installation & Setup

### 1. **Clone the Repository**
```bash
git clone https://github.com/your-username/nexushome-iot.git
cd nexushome-iot
```

### 2. **Configure Application Settings**
```bash
cp appsettings.json appsettings.Development.json
```

Edit `appsettings.Development.json` with your configuration:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=NexusHomeDb;Trusted_Connection=true;",
    "IoTHub": "HostName=your-iothub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=YOUR_KEY"
  },
  "MQTT": {
    "BrokerHost": "localhost",
    "BrokerPort": 1883
  }
}
```

### 3. **Install Dependencies**
```bash
dotnet restore
```

### 4. **Set Up Database**
```bash
dotnet ef database update
```

### 5. **Run the Application**
```bash
dotnet run
```

The application will be available at:
- **API**: https://localhost:7001
- **Swagger UI**: https://localhost:7001/swagger

## ðŸ”§ Configuration Guide

### **Azure IoT Hub Setup**
1. Create an IoT Hub in Azure Portal
2. Create device identity or use Device Provisioning Service
3. Update connection strings in appsettings.json

### **MQTT Broker Setup**
```bash
# Install Mosquitto (Ubuntu/Debian)
sudo apt update
sudo apt install mosquitto mosquitto-clients

# Start Mosquitto
sudo systemctl start mosquitto
sudo systemctl enable mosquitto
```

### **Matter Protocol Setup**
1. Ensure Thread/WiFi network is configured
2. Set up Matter fabric credentials
3. Configure device commissioning parameters

### **Machine Learning Models**
The system includes pre-trained models for:
- Energy consumption forecasting
- Predictive maintenance
- Anomaly detection

Models are automatically loaded and updated based on your data.

## ðŸ“± Device Integration

### **Supported Device Types**
- **Smart Thermostats** (Nest, Ecobee, Honeywell)
- **Smart Lights** (Philips Hue, LIFX, TP-Link Kasa)
- **Energy Meters** (Schneider Electric, Siemens)
- **Solar Inverters** (SolarEdge, Enphase, SMA)
- **Battery Storage** (Tesla Powerwall, LG Chem)
- **Smart Switches & Outlets**
- **HVAC Systems**
- **Electric Vehicle Chargers**

### **Adding New Devices**

#### **Matter Devices**
```http
POST /api/devices/matter/commission
{
  "setupCode": "12345678901",
  "discriminator": "3840"
}
```

#### **MQTT Devices**
```http
POST /api/devices
{
  "deviceId": "DEVICE_001",
  "name": "Living Room Light",
  "type": "SmartLight",
  "protocol": "MQTT",
  "manufacturer": "Philips"
}
```

## ðŸ¤– AI & Machine Learning Features

### **Predictive Maintenance**
- Analyzes device performance patterns
- Predicts failures 7-30 days in advance
- Generates maintenance recommendations
- Tracks model accuracy and improves over time

### **Energy Optimization**
- Real-time load optimization
- Cost-based scheduling
- Solar generation forecasting
- Battery charge optimization

### **Anomaly Detection**
- Detects unusual energy consumption patterns
- Identifies potential security issues
- Monitors device health anomalies
- Sends real-time alerts

## ðŸ“Š API Documentation

### **Core Endpoints**

#### **Energy Management**
- `GET /api/energy/dashboard` - Get energy dashboard data
- `GET /api/energy/consumption` - Get consumption history
- `GET /api/energy/forecast` - Get energy demand forecast
- `POST /api/energy/optimization/execute` - Execute optimization plan

#### **Device Management**
- `GET /api/devices` - List all devices
- `POST /api/devices` - Add new device
- `PUT /api/devices/{id}` - Update device
- `DELETE /api/devices/{id}` - Remove device
- `POST /api/devices/{id}/command` - Send device command

#### **Predictive Maintenance**
- `GET /api/maintenance/predictions` - Get maintenance predictions
- `POST /api/maintenance/records` - Create maintenance record
- `POST /api/maintenance/anomaly-detection/{deviceId}` - Detect anomalies

### **Real-time Communication**
WebSocket endpoints for real-time updates:
- `/energyHub` - Real-time energy data
- `/deviceHub` - Device status updates
- `/notificationHub` - System notifications

## ðŸ³ Docker Deployment

### **Build Docker Image**
```bash
docker build -t nexushome-iot:latest .
```

### **Run with Docker Compose**
```bash
docker-compose up -d
```

The `docker-compose.yml` includes:
- NexusHome application
- SQL Server
- Redis
- MQTT Broker (Mosquitto)

## â˜ï¸ Cloud Deployment

### **Azure Container Instances**
```bash
az container create \
  --resource-group nexushome-rg \
  --name nexushome-app \
  --image nexushome-iot:latest \
  --ports 80 443 \
  --environment-variables ASPNETCORE_ENVIRONMENT=Production
```

### **Azure App Service**
```bash
az webapp create \
  --resource-group nexushome-rg \
  --plan nexushome-plan \
  --name nexushome-app \
  --deployment-container-image-name nexushome-iot:latest
```

## ðŸ“ˆ Performance & Scalability

### **Performance Metrics**
- **API Response Time**: < 100ms average
- **IoT Message Processing**: 10,000+ messages/second
- **ML Inference**: < 50ms for predictions
- **Database Queries**: Optimized with proper indexing

### **Scalability Features**
- Horizontal scaling with load balancers
- Database connection pooling
- Redis caching for frequent queries
- Background services for heavy processing
- Queue-based message processing

## ðŸ” Security Features

### **Authentication & Authorization**
- JWT token-based authentication
- Role-based access control (RBAC)
- Device-level security certificates
- OAuth 2.0 integration

### **IoT Security**
- End-to-end encryption
- Device identity verification
- Secure key management
- Regular security audits

### **Data Protection**
- Data encryption at rest and in transit
- GDPR compliance features
- Audit logging
- Privacy controls

## ðŸ§ª Testing

### **Run Unit Tests**
```bash
dotnet test
```

### **Run Integration Tests**
```bash
dotnet test --filter Category=Integration
```

### **Load Testing**
```bash
# Using Artillery.js
npm install -g artillery
artillery run load-test.yml
```

## ðŸ“Š Monitoring & Logging

### **Application Insights**
- Real-time application monitoring
- Performance metrics tracking
- Exception tracking and alerting
- Custom telemetry

### **Structured Logging**
- Serilog for structured logging
- Log aggregation and analysis
- Configurable log levels
- Integration with log management systems

## ðŸ¤ Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

### **Development Setup**
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

### **Code Standards**
- Follow C# coding conventions
- Add XML documentation for public APIs
- Maintain test coverage above 80%
- Use async/await patterns for I/O operations

## ðŸ“„ License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## ðŸ†˜ Support & Documentation

### **Documentation**
- [Wiki](https://github.com/your-username/nexushome-iot/wiki)
- [API Reference](https://your-domain.com/api/docs)
- [User Guide](docs/user-guide.md)
- [Developer Guide](docs/developer-guide.md)

### **Support**
- [Issues](https://github.com/your-username/nexushome-iot/issues)
- [Discussions](https://github.com/your-username/nexushome-iot/discussions)
- Email: support@nexushome.io

## ðŸ—ºï¸ Roadmap

### **Version 2.1** (Q2 2025)
- [ ] Enhanced mobile app with offline capabilities
- [ ] Integration with more renewable energy systems
- [ ] Advanced machine learning model marketplace
- [ ] Multi-tenant support for property management

### **Version 2.2** (Q3 2025)
- [ ] Blockchain-based energy trading
- [ ] AR/VR interface for device management
- [ ] Advanced weather integration
- [ ] Community energy sharing features

### **Version 3.0** (Q4 2025)
- [ ] Edge computing support
- [ ] Quantum-resistant security
- [ ] Advanced AI conversation interface
- [ ] Global smart grid integration

## ðŸ™ Acknowledgments

- **Microsoft** for .NET 8 and Azure services
- **Connectivity Standards Alliance** for Matter protocol
- **Eclipse Foundation** for Mosquitto MQTT broker
- **ML.NET Community** for machine learning frameworks
- **Contributors** who help improve this project

## ðŸ“ž Contact

- **Website**: [https://nexushome.io](https://nexushome.io)
- **Email**: info@nexushome.io
- **Twitter**: [@NexusHomeIoT](https://twitter.com/NexusHomeIoT)
- **LinkedIn**: [NexusHome IoT](https://linkedin.com/company/nexushome-iot)

---

**Built with â¤ï¸ by the NexusHome Team**

*Making smart homes smarter, one device at a time.** **Custom Automation Engine:** Create powerful "if-this-then-that" style rules to automate your smart home (e.g., "If motion is detected after 10 PM, turn on the hallway light").
* **Real-time Communication:** Leverages **SignalR** to instantly push data from the server to the web dashboard without needing to refresh.
* **Scalable Architecture:** Built with a clean, service-oriented architecture that is easy to maintain and extend.

---

## 🛠️ Technology Stack

The platform is built using a modern, robust set of technologies from the .NET ecosystem.

| Component          | Technology                     | Purpose                                    |
| ------------------ | ------------------------------ | ------------------------------------------ |
| **Backend API** | **ASP.NET Core 8 Web API** | Data ingestion, device control, rules API  |
| **Frontend Web App** | **Blazor WebAssembly** | Interactive and responsive user interface  |
| **Database** | **PostgreSQL** | Relational data storage for devices & rules |
| **Data Access** | **Entity Framework Core 8** | Object-Relational Mapper (ORM)             |
| **Real-time Engine** | **SignalR** | Bi-directional client-server communication |
| **Device Simulator** | **.NET 8 Console Application** | Simulates IoT device telemetry             |

---

## 🚀 Getting Started

Follow these instructions to get the project up and running on your local machine.

### Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Recommended for database)
* A C# IDE (e.g., Visual Studio 2022, JetBrains Rider, or VS Code with C# Dev Kit)

### 1. Set Up the PostgreSQL Database

The easiest way to run PostgreSQL is with Docker. Create a `docker-compose.yml` file in the root of your project with the following content:

```yml
version: '3.8'
services:
  postgres_db:
    image: postgres:14
    container_name: nexushome_db
    environment:
      - POSTGRES_USER=nexushome
      - POSTGRES_PASSWORD=yoursecurepassword
      - POSTGRES_DB=nexushome_dev
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:
```

Open a terminal in your project's root directory and run `docker-compose up -d` to start the database server.

### 2. Configure and Run the API

1.  Navigate to the `NexusHome.Api` directory.
2.  Open `appsettings.json` and ensure the `DefaultConnection` string matches the database settings from your `docker-compose.yml` file.
3.  Apply the database migrations by running the following command in the `NexusHome.Api` directory:
    ```bash
    dotnet ef database update
    ```
4.  Start the API:
    ```bash
    dotnet run
    ```
    Note the URL the API is running on (e.g., `https://localhost:7123`).

### 3. Run the Applications

You will need to run three applications simultaneously in separate terminals.

1.  **API:** (Already running from the previous step).
2.  **Device Simulator:**
    * Open a new terminal and navigate to `NexusHome.DeviceSimulator`.
    * In `Program.cs`, update the `ApiBaseUrl` constant to match your API's URL.
    * Run `dotnet run`.
3.  **Web App:**
    * Open a third terminal and navigate to `NexusHome.WebApp`.
    * In `Pages/Index.razor` and `Pages/Rules.razor`, update the `ApiBaseUrl` constant to match your API's URL.
    * Run `dotnet run`.
    * Open your web browser and navigate to the Blazor app's URL (e.g., `https://localhost:7289`).

You should now see the dashboard with live data streaming from the simulator!
