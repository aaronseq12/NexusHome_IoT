# NexusHome: IoT Smart Home Dashboard & Automation Platform

![Project Status](https://img.shields.io/badge/status-in%20development-yellow.svg)
![.NET Version](https://img.shields.io/badge/.NET-8.0-blueviolet.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

NexusHome is a modern IoT platform built with C# and ASP.NET Core. It provides a real-time web dashboard to monitor and control simulated smart home devices and features a powerful, user-configurable automation rules engine.

---

## ✨ Core Features

* **Live Dashboard:** A dynamic web interface built with Blazor that displays real-time telemetry from all connected devices.
* **Device Simulation:** A flexible console application that realistically simulates various IoT devices like thermostats, lights, and motion sensors.
* **Custom Automation Engine:** Create powerful "if-this-then-that" style rules to automate your smart home (e.g., "If motion is detected after 10 PM, turn on the hallway light").
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
