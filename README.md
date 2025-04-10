# Task Scheduling API

A .NET 8 backend API for scheduling, managing, and notifying users about tasks via email. Built with ASP.NET Core, Entity Framework Core (Postgres), and a background service for task execution.

## Features
- **Task Types**: OneOff (runs once), Delayed (runs after a delay), Recurring (runs on a schedule—hourly/daily).
- **CRUD Operations**: Create, read, update, delete tasks via RESTful endpoints.
- **Authentication**: JWT-based, with email confirmation policy.
- **Background Processing**: Schedules and executes tasks, sends email notifications on completion.
- **API Docs**: Swagger UI at `/swagger` for interactive testing.
  

## Tech Stack
- **Framework**: .NET 8, ASP.NET Core
- **Database**: Postgres (via EF Core)
- **Auth**: ASP.NET Core Identity, JWT
- **Background Tasks**: Hosted Service (`TaskNotificationService`)
- **Email**: SMTP (Gmail)
- **Docs**: Swagger/OpenAPI


## Enum Reference

### `TaskType`
Used in `POST /api/task` → `Type` field:

- `0` = **OneOff** – Runs once at the specified time.
- `1` = **Delayed** – Runs after a delay.
- `2` = **Recurring** – Repeats based on `RecurrenceInterval` + `RecurrenceUnit`.

### `RecurrenceUnit`
Used when `TaskType` is `Recurring`:

- `"s"` = Seconds  
- `"m"` = Minutes  
- `"h"` = Hours  
- `"d"` = Days

   
## Setup (Local)
### Prerequisites
- .NET 8 SDK
- Postgres (e.g., local or Docker)
- SMTP service (e.g., Gmail with app password)

  ### Steps
1. **Clone**:
   ```bash
   git clone https://github.com/amxrac/Task-Scheduling-API.git
   cd Task-Scheduling-API
   ```
2. **Config**:
     - Create `appsettings.json` in the project root (not tracked—see .gitignore)
     - Add your Postgres, SMTP, and JWT settings:
       ```json
       {
        "ConnectionStrings": { "TSDB": "Host=localhost;Database=TSDB;Username=postgres;Password=your-pass" },
        "EmailConfig": { "Host": "smtp.gmail.com", "Port": 465, "Email": "your-email@gmail.com", "Password": "your-app-pass" },
        "JwtSettings": { "Issuer": "http://localhost:5064", "Audience": "http://localhost:5064", "Key": "your-long-secret-key" },
        "AdminConfig": { "Email": "admin@admin.com", "Password": "SecurePassword123!" }
        }
       ```
3. **Restore and Run**:
     ```bash
     dotnet restore
    dotnet ef database update  # If migrations exist
    dotnet run
     ```
4. **Test**:
  - Swagger: http://localhost:5064/swagger
  - Login via POST /api/auth/login, use JWT for protected endpoints (e.g., POST /api/task)


## Usage
- **Create Task**: `POST /api/task` (e.g., `{ "Title": "Backup", "Type": 2, "ScheduledAt": "2025-04-11T10:00:00Z", "RecurrenceInterval": 1, "RecurrenceUnit": "h" }`)
- **List Tasks**: `GET /api/task` (Admin only)
- Notifications: Emails sent on task completion (retries up to 3)


## Project Structure
- `Controllers/TaskController.cs`: REST endpoints TO CREATE, READ, UPDATE and (soft) DELETE tasks
- `Controllers/AuthController.cs`:  REST endpoints TO CREATE, READ and UPDATE user accounts
- `Data/AppDbContext.cs`: EF Core setup
- `Services/TaskNotificationService.cs`: Background task scheduler
- `Models/ScheduledTask.cs`: Task entity


## License
MIT














   
   
