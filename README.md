NexusHome: IoT Smart Home Dashboard & Automation PlatformNexusHome is a comprehensive IoT platform built with C# and ASP.NET Core. It provides a web-based dashboard to monitor and control simulated smart home devices and features a custom automation rules engine.Project ArchitectureThe platform is built using a service-oriented approach, consisting of three main projects:NexusHome.Api (ASP.NET Core Web API)Data Ingestion: A high-performance endpoint to receive telemetry data from devices.Real-time Hub: A SignalR hub that pushes live data to connected web clients.Rules Engine: A background service that constantly evaluates user-defined automation rules against incoming data.Device Control API: Secure endpoints for controlling device states (e.g., turning a light on/off).Database Layer: Uses Entity Framework Core to interact with the PostgreSQL database.NexusHome.DeviceSimulator (C# Console App)Acts as a collection of virtual smart devices (thermostats, lights, motion sensors).Periodically sends randomized, realistic telemetry data (temperature, brightness, motion status) to the NexusHome.Api via HTTP requests.NexusHome.WebApp (Blazor WebAssembly)Live Dashboard: Displays real-time status and data from all registered devices.Rules Management: A user interface for creating, viewing, and deleting automation rules.Communicates with the backend via HTTP for fetching data and SignalR for real-time updates.Technology StackBackend: .NET 8, ASP.NET Core, SignalR, Entity Framework CoreFrontend: Blazor WebAssemblyDatabase: PostgreSQLReal-time Communication: SignalRArchitecture: Service-Oriented, RESTful APIFile Structure/NexusHome
├── NexusHome.Api/
│   ├── Controllers/
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   ├── Hubs/
│   │   └── TelemetryHub.cs
│   ├── Models/
│   │   ├── Device.cs
│   │   ├── AutomationRule.cs
│   │   └── TelemetryData.cs
│   ├── Services/
│   │   ├── RulesEngineService.cs
│   │   └── DeviceService.cs
│   ├── appsettings.json
│   └── Program.cs
├── NexusHome.DeviceSimulator/
│   ├── Program.cs
│   └── DeviceSimulator.cs
├── NexusHome.WebApp/
│   ├── Pages/
│   │   ├── Index.razor
│   │   └── Rules.razor
│   ├── Shared/
│   ├── wwwroot/
│   └── Program.cs
├── .gitignore
└── README.md
Setup and InstallationPrerequisites.NET 8 SDKDocker and Docker Compose (Recommended for easy database setup)A C# IDE (Visual Studio, VS Code, JetBrains Rider)1. Set up PostgreSQL DatabaseThe easiest way to run PostgreSQL is with Docker. Create a docker-compose.yml file in the root of your project with the following content:version: '3.8'
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
Run docker-compose up -d in your terminal to start the database server.2. Configure the APINavigate to the NexusHome.Api directory.Open appsettings.json.Update the DefaultConnection string to match the database settings from the docker-compose.yml file:"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=nexushome_dev;Username=nexushome;Password=yoursecurepassword"
}
Run the database migrations:dotnet ef database update
3. Running the ApplicationStart the API:Open a terminal, navigate to NexusHome.Api, and run dotnet run.The API will be listening on a specified port (e.g., https://localhost:7123).Start the Device Simulator:Open a new terminal, navigate to NexusHome.DeviceSimulator.Update the ApiBaseUrl in DeviceSimulator.cs to match the API's URL.Run dotnet run. You should see console output indicating that devices are sending data.Launch the Web App:Open a third terminal, navigate to NexusHome.WebApp.Run dotnet run.Open your web browser and navigate to the address provided (e.g., https://localhost:7289). You should see the dashboard with live data appearing.
