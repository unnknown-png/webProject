<!-- markdownlint-disable MD033 -->

# WebProject - Matrix Solver with Real-Time Progress and Load Balancing ⚡

## Overview 🧠
WebProject is an ASP.NET Core MVC application that solves systems of linear equations using Gaussian elimination and LU decomposition. It provides real-time progress updates via SignalR, asynchronous task processing via Redis-backed queues, and optional load balancing through Nginx.

This repository is designed as a portfolio-grade project that demonstrates backend architecture, distributed processing, and a production-like local setup.

## Features ✨
- User authentication with cookie-based sessions and BCrypt password hashing.
- Solve linear systems (NxN) with Gaussian elimination and LU decomposition.
- Real-time progress updates and task status events via SignalR.
- Background worker that processes queued tasks from Redis.
- Task cancellation and per-user concurrency limits.
- History of completed calculations with export and cleanup endpoints.
- Optional load balancing across two app instances via Nginx.

## Tech Stack 🧰
- **Backend:** ASP.NET Core MVC (.NET 9)
- **Data:** PostgreSQL, Entity Framework Core
- **Realtime:** SignalR (Redis backplane)
- **Queue and cache:** Redis (StackExchange.Redis)
- **Frontend:** Razor views + vanilla JS
- **Reverse proxy:** Nginx (optional, local load balancing)

## Architecture (High Level) 🏗️
```
Client (Browser)
  |  HTTP + SignalR
  v
Nginx (optional)
  |  / -> ASP.NET instances (5001, 5002)
  |  /progressHub -> SignalR instance
  v
ASP.NET Core MVC
  |  Controllers (Account, Matrix, History, Home)
  |  Services (Gaussian, LU, Combined)
  |  Background Worker (MatrixWorker)
  |  EF Core DbContext (Users, CalculationHistory, DataProtectionKeys)
  v
PostgreSQL  <->  Redis (queue + matrix cache)
```

### Core Components 🧩
- **`Program.cs`**: DI setup, middleware, SignalR, Redis, EF Core, hosted worker.
- **Controllers:**
  - `AccountController` for login/register and cookie auth.
  - `MatrixController` for matrix generation, solving, queuing, status, cancel.
  - `HistoryController` for history retrieval, export, and cleanup.
  - `HomeController` for UI entry.
- **Services:**
  - `GaussianEliminationService`, `LUDecompositionService`, `CombinedMatrixService`.
  - `RedisQueueService` for task queue and matrix cache.
  - `MatrixWorker` background processor.
  - `TaskManager` for per-user concurrency and cancellation tokens.
- **Realtime:** `ProgressHub` publishes progress and task events.
- **Middleware:** `ServerLoggingMiddleware` logs API and SignalR traffic.

## Public API (Key Routes) 🔌
- `POST /api/matrix/solve`
- `POST /api/matrix/solve-stored`
- `POST /api/matrix/queue-solve`
- `POST /api/matrix/generate`
- `GET /api/matrix/task-status/{taskId}`
- `GET /api/matrix/my-tasks`
- `GET /api/matrix/queue-stats`
- `POST /api/matrix/cancel/{taskId}`
- `GET /api/history`
- `DELETE /api/history`
- `GET /api/history/export`
- `GET /progressHub` (SignalR hub)

## Data Flow (Typical Request) 🔄
1. User submits a matrix from the UI.
2. The matrix is cached in Redis and a task is pushed to the queue.
3. `MatrixWorker` dequeues the task and runs Gaussian + LU in parallel.
4. Progress is streamed to the client via SignalR.
5. Results and history are stored in PostgreSQL.

## Local Development 🚀

### Prerequisites ✅
- .NET 9 SDK
- PostgreSQL running on `localhost:5432`
- Redis running on `localhost:6379`
- LibMan (`libman`) for client library restore
- Nginx (optional, for load balancing)

### Restore and Setup 🛠️
```bash
# From the repository root
dotnet restore

# Restore SignalR client files
cd webProject
libman restore

# Apply EF Core migrations
dotnet ef database update
```

### Run Single Instance ▶️
```bash
# From the repository root
dotnet run --project webProject
```

### Run Two Instances (Load Balancing) ⚖️
```bash
# From the repository root
./start-server-5001.sh
./start-server-5002.sh
```

### Start Nginx with Local Config (Optional) 🌐
```bash
# From the repository root
sudo /opt/homebrew/opt/nginx/bin/nginx -t -c "$(pwd)/nginx.conf"
sudo /opt/homebrew/opt/nginx/bin/nginx -c "$(pwd)/nginx.conf"
```

## Configuration ⚙️
- Connection string lives in `webProject/appsettings.json` and `webProject/appsettings.Development.json`.
- For production, move secrets to environment variables or user secrets.
- Optional server metadata can be passed via `ServerInfo:ServerName` and `ServerInfo:Port`.

## Project Structure 🗂️
- `webProject/Program.cs`
- `webProject/Controllers/`
- `webProject/Services/`
- `webProject/Models/`
- `webProject/Data/`
- `webProject/Views/`
- `webProject/wwwroot/`
- `nginx.conf`
- `start-server-5001.sh`, `start-server-5002.sh`

## Notes 📝
- This project uses a Redis-backed task queue and SignalR backplane for real-time progress.
- For local load balancing, run two app instances and start Nginx with `nginx.conf`.

## Author 🙋‍♂️
Andriy Kakhnovets   
Faculty of Applied Mathematics and Informatics, Ivan Franko National University of Lviv
