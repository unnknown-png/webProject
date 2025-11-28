# Web Service

## Project Overview
This web application is designed to perform computationally intensive tasks — specifically solving systems of linear equations. The application supports load balancing across multiple application servers to ensure stable operation under high request loads.

## Key Features
- Limits on problem complexity (by execution time or number of unknowns).
- Real-time progress reporting for running computations.
- History of completed calculations and current task status.
- Ability to cancel or start new tasks.
- User authentication.
- Load balancing across multiple application servers (minimum two).

## How to Run (local development)
Prerequisites:
- .NET 9 SDK (or compatible .NET 9 runtime)
- (Optional) Entity Framework Core tools if you need to apply migrations: `dotnet tool install --global dotnet-ef`

Run steps (from the repository root or `webProject` folder):

1. Restore packages:
```
dotnet restore
```
2. Apply database migrations (if you want to initialize the database):
```
dotnet ef database update --project webProject
```
3. Build and run the application:
```
dotnet build
dotnet run --project webProject
```

By default the app will listen on the ports configured in `Properties/launchSettings.json` or the environment. Use browser to open the configured URL (usually `https://localhost:5001` or similar).

## Project Structure (important files)
- `Program.cs` — application entry point and host configuration.
- `Controllers/` — ASP.NET MVC controllers (e.g., `MatrixController.cs`, `AccountController.cs`).
- `Services/` — background or helper services (e.g., `GaussianEliminationService.cs`).
- `Data/` — `ApplicationDbContext.cs` and EF Core migrations under `Migrations/`.
- `Views/` — Razor views for the UI.

## Notes
- This application is intended for demonstration and academic use. For production deployments, configure HTTPS, secure secrets (e.g., connection strings), and set up a proper load balancer (NGINX, HAProxy, cloud LB) and monitoring.
- If you plan to run multiple instances for load balancing, ensure that any in-memory state is moved to a shared store (database, distributed cache) or use sticky sessions as appropriate.

## Author
Andriy Kakhnovets
Faculty of Applied Mathematics and Informatics, Ivan Franko National University of Lviv
