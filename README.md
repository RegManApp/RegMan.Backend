# RegMan.Backend

RegMan backend is the **ASP.NET Core Web API** that enforces registration rules and exposes the HTTP + SignalR surface used by the frontend.

- Full documentation (source of truth): https://github.com/RegManApp/RegMan.docs
- Frontend repo: https://github.com/RegManApp/RegMan.Frontend
- Live site: https://regman.app

## Tech Stack

- ASP.NET Core (.NET 8)
- Entity Framework Core + SQL Server
- ASP.NET Identity
- JWT authentication + role-based authorization (Admin/Student/Instructor)
- SignalR (realtime chat + notifications)

## Architecture Layers

The backend is structured as:

- **API** (`RegMan.Backend.API/`): controllers, auth, middleware, Swagger, SignalR hubs
- **Business Layer** (`RegMan.Backend.BusinessLayer/`): services and business rules
- **DAL** (`RegMan.Backend.DAL/`): EF Core entities, repositories, Unit of Work, migrations

## Clone & Run Locally

```bash
git clone https://github.com/RegManApp/RegMan.Backend
cd RegMan.Backend

dotnet restore

dotnet ef database update

dotnet run
```

Notes:

- If you run from the repo root, you may prefer: `dotnet run --project RegMan.Backend.API/RegMan.Backend.API.csproj`.
- If `dotnet ef database update` fails from the repo root, run:

  ```bash
  dotnet ef database update --project RegMan.Backend.DAL/RegMan.Backend.DAL.csproj --startup-project RegMan.Backend.API/RegMan.Backend.API.csproj
  ```

- Default dev URLs (from launch settings): `http://localhost:5236` and `https://localhost:7025`.
- Swagger UI is available at `/swagger`.

## Environment Variables

Required:

- `ConnectionStrings__DefaultConnection` (SQL Server connection string)
- `Jwt__Key` (JWT signing key, **>= 32 characters**)

Optional (enables Google Calendar integration; otherwise it is disabled):

- `GOOGLE_CLIENT_ID`
- `GOOGLE_CLIENT_SECRET`
- `GOOGLE_REDIRECT_URI`

## Database & Migrations

- EF Core migrations are stored under `RegMan.Backend.DAL/Migrations/`.
- The API applies migrations at startup (`Database.MigrateAsync()`), but the repo also supports manual migration execution via `dotnet ef database update`.

If `dotnet ef` is not available, install it:

```bash
dotnet tool install --global dotnet-ef
```

## Full Documentation

Deep technical docs (architecture rationale, auth details, full API reference) live in:

- https://github.com/RegManApp/RegMan.docs
